using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using VRageMath;

namespace Scripts.KSWH
{
    class EnemyShip
    {
        public Sandbox.ModAPI.IMyCubeGrid myShip;
        private IMyControllableEntity m_cockpit;
        public IMyEntity Target;

        private Vector3 previousMyPosition;
        private Vector3 previousTargetPosition;

        public float Range = 10000;
        public float RunOrver = 500;
        public float SizeFactor = 200;
        public float RotateFactor = 3;

        private String TargetName = "";

        // TODO how can i get random numbers?
        float destPMFactorX;
        float destPMFactorY;
        float destPMFactorZ;

        public EnemyShip(IMyCubeGrid ship)
        {
            myShip = ship;

            string shipname = myShip.DisplayName;
            var shipcommands = shipname.Split(new char[] { ' ' });
            foreach (String command in shipcommands)
            {
                var words = command.Split(new char[] { '=' });
                if (words.Length != 2) continue;
                switch (words[0])
                {
                    case "size":
                        float factor = float.Parse(words[1]);
                        this.SizeFactor = factor;
                        break;
                    case "rader":
                        float factor2 = float.Parse(words[1]);
                        this.Range = factor2;
                        break;
                    case "rotate":
                        float factor3 = float.Parse(words[1]);
                        this.RotateFactor = factor3;
                        break;
                    case "runover":
                        float factor4 = float.Parse(words[1]);
                        this.RunOrver = factor4;
                        break;
                }
            }

            GetCockpit();
            if (m_cockpit != null)
                m_cockpit.SwitchDamping();

            //生成位置を元にしたニセランダム補正
            destPMFactorX = -1 * Math.Sign(myShip.GetPosition().X);
            destPMFactorY = -1 * Math.Sign(myShip.GetPosition().Y);
            destPMFactorZ = -1 * Math.Sign(myShip.GetPosition().Z);
            if (destPMFactorX == 0) destPMFactorX = 1;
            if (destPMFactorY == 0) destPMFactorY = 1;
            if (destPMFactorZ == 0) destPMFactorZ = 1;

            //DisableOverride();

            Invaders.Talk(myShip.DisplayName, "AI readied.");
        }

        private void GetCockpit()
        {
            var lst = new List<IMySlimBlock>();
            //myShip.GetBlocks(lst, (x) => x.FatBlock is IMyControllableEntity && x.FatBlock.IsWorking);
            //myShip.GetBlocks(lst, (x) => x.FatBlock is IMyControllableEntity);
            myShip.GetBlocks(lst, (x) => x.FatBlock is IMyControllableEntity
                && x.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Cockpit));
            if (lst == null || lst.Count == 0)
            {
                m_cockpit = null;
                return;
            }

            m_cockpit = lst[0].FatBlock as IMyControllableEntity;
        }

        public void Update(int count)
        {

            //if (count % 10 != 0)
            //    return;

            GetCockpit();

            if (!IsWorkingInvader())
            {
                Invaders.Talk(myShip.DisplayName, "not working");
                return;
            }

            if (count % 100 == 0)
                UpdateTarget();
            if (Target == null)
            {
                Invaders.Talk(myShip.DisplayName, "missing target");
                return;
            }

            TargetName = Target.DisplayName;
            if (TargetName == "" || TargetName == null)
                TargetName = "Enemy";

            //目標の位置を取得
            Vector3 dest = Target.GetPosition();

            // TODO 自機及び目標の速度による補正

            // TODO 自機及び目標の大きさを考慮
            dest = CheckObstacle(dest, SizeFactor);

            // TODO 進路上に何かある場合、回避

            //目標までのベクトル
            //var dir = Target.GetPosition() - myShip.GetPosition();
            Vector3 dir = (dest - myShip.GetPosition()) * RunOrver;
            //var dir = dest - new Vector3((float)myShip.GetPosition().X, (float)myShip.GetPosition().Y, (float)myShip.GetPosition().Z);
            //if (dir.Length() < 10)
            //{
            //    MissionComponent.Talk(Ship.DisplayName, "Nearby " + TargetName);
            //    return;
            //}

            //目標への単位ベクトル（傾き）
            //var dirNorm = Vector3.Normalize(dir);
            var dirNorm = Vector3.Normalize(Target.GetPosition() - myShip.GetPosition());
            //自機の対ワールド相対座標から、目標への傾きを算出
            var x = -(m_cockpit as IMyEntity).WorldMatrix.Up.Dot(dirNorm);
            var y = -(m_cockpit as IMyEntity).WorldMatrix.Left.Dot(dirNorm);
            var forw = (m_cockpit as IMyEntity).WorldMatrix.Forward.Dot(dirNorm);

            //目標が背後の場合、縦方向の回頭を停止
            if (forw < 0)
            {
                y = 0;
            }
            else
            {
                //目標が背後の場合はとりあえず回頭止めない

                //誤差が小さい場合無視
                if (Math.Abs(x) < 0.1f)
                {
                    x = 0;
                }
                if (Math.Abs(y) < 0.1f)
                {
                    y = 0;
                }
            }

            //進行ベクトルを生成
            dir = Vector3.TransformNormal(dir, (m_cockpit as IMyEntity).GetWorldMatrixNormalizedInv());
            
            //回頭ベクトルを生成
            var rot = new Vector2((float)x, (float)y) * RotateFactor;

            Invaders.Talk(myShip.DisplayName, "dir=" + dir + "/ rot=" + rot);
            if (dir.LengthSquared() > 0 || rot.LengthSquared() > 0)
            {
                //動かば動け
                Invaders.Talk(myShip.DisplayName, "apro: "
                    + TargetName + "[@" + dir.Length() + " / " + rot.X + " , " + rot.Y + " > " + (dest - Target.GetPosition()).Length() + "]");
                m_cockpit.MoveAndRotate(dir, rot, 0);
            }
            else
            {
                //停止
                Invaders.Talk(myShip.DisplayName, "keep: " + TargetName + "[" + (dest - Target.GetPosition()).Length() + "]");
                m_cockpit.MoveAndRotateStopped();
            }
        }

        private Vector3 CheckObstacle(Vector3 dest, float sizeFactor)
        {

            //生成位置を元にしたニセランダム補正
            dest.X += sizeFactor * destPMFactorX;
            dest.Y += sizeFactor * destPMFactorY;
            dest.Z += sizeFactor * destPMFactorZ;
            while (true)
            {
                //目標地点周辺に何かある場合、再度補正
                var sphere = new BoundingSphereD(dest, sizeFactor);
                var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                if (ents == null || ents.Count == 0)
                    break;
                dest.X += sizeFactor * destPMFactorX;
                dest.Y += sizeFactor * destPMFactorY;
                dest.Z += sizeFactor * destPMFactorZ;

            }
            return dest;
        }

        private void UpdateTarget()
        {

            if (Target != null)
            {
                // 稼働中の船を追跡中なら再探索しない
                // 停止中の船を追跡中なら再探索する
                // プレイヤーを追跡中なら再探索する

                var ship = Target as IMyCubeGrid;

                if (ship != null
                    && IsWorkingShip(ship))
                {
                    // target is living ship
                    return;
                }
                
            }

            //search next target
            var bs = new BoundingSphereD(myShip.GetPosition(), Range);
            var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref bs);

            foreach (var entity in ents)
            {
                var nextShip = entity as IMyCubeGrid;
                if (nextShip != null)
                {
                    // 一番近いEntityを目標にする
                    // Invaderは除く
                    // 稼働している船のみ
                    if (
                        nextShip.DisplayName == null
                        ||
                        (nextShip.DisplayName != null && !nextShip.DisplayName.Contains(Invaders.Identifier))
                        )
                    {
                        Target = entity;
                        break;
                    }
                }
                //else
                //{
                //    // target player?
                //}
            }
        }

        public bool IsWorkingInvader()
        {
            if (!myShip.DisplayName.Contains(Invaders.Identifier))
            {
                Invaders.Talk(myShip.DisplayName, "I'm not a Invader.");
                return false;
            }

            if (m_cockpit == null)
            {
                Invaders.Talk(myShip.DisplayName, "lost control.");
                return false;
            }

            if (!IsWorkingShip(myShip))
            {
                Invaders.Talk(myShip.DisplayName, "no power.");
                return false;
            }

            return true;
        }

        private bool IsWorkingShip(IMyCubeGrid ship)
        {

            var reactors = new List<IMySlimBlock>();
            var reactors2 = new List<IMySlimBlock>();
            var reactors3 = new List<IMySlimBlock>();
            var reactors4 = new List<IMySlimBlock>();
            MyDefinitionId reactorDef = new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockSmallGenerator");
            MyDefinitionId reactorDef2 = new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockLargeGenerator");
            MyDefinitionId reactorDef3 = new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockSmallGenerator");
            MyDefinitionId reactorDef4 = new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockLargeGenerator");
            ship.GetBlocks(reactors, (x) => x.FatBlock != null && x.FatBlock.BlockDefinition == reactorDef);
            ship.GetBlocks(reactors2, (x) => x.FatBlock != null && x.FatBlock.BlockDefinition == reactorDef2);
            ship.GetBlocks(reactors3, (x) => x.FatBlock != null && x.FatBlock.BlockDefinition == reactorDef3);
            ship.GetBlocks(reactors4, (x) => x.FatBlock != null && x.FatBlock.BlockDefinition == reactorDef4);
            //reactors.Concat(reactors2);
            //reactors.Concat(reactors3);
            //reactors.Concat(reactors4);

            bool hasEnergy = false;
            foreach (var reactor in reactors)
            {
                if (reactor != null)
                    hasEnergy |= reactor.FatBlock.IsWorking;
            }
            foreach (var reactor in reactors2)
            {
                if (reactor != null)
                    hasEnergy |= reactor.FatBlock.IsWorking;
            }
            foreach (var reactor in reactors3)
            {
                if (reactor != null)
                    hasEnergy |= reactor.FatBlock.IsWorking;
            }
            foreach (var reactor in reactors4)
            {
                if (reactor != null)
                    hasEnergy |= reactor.FatBlock.IsWorking;
            }
            return hasEnergy;
        }
    }
}
