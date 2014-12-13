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
    [Sandbox.Common.MySessionComponentDescriptor(Sandbox.Common.MyUpdateOrder.BeforeSimulation)]
    class Invaders : Sandbox.Common.MySessionComponentBase
    {
        public static String Identifier = "-Invader";
        public static String FriendIdentifier = "+Invader";

        private static bool WantTalk = false;

        class MissionData
        {
            public List<EnemyShip> EnemyShips = new List<EnemyShip>();
            public List<IMyCubeGrid> CheckedShips = new List<IMyCubeGrid>();
            public DateTime StartTime;

            public int EnemyEncounted = 0;
            public int kill = 0;
        }

        public static MyLogger Logger;

        private MissionData m_data;

        private bool m_loaded;
        private bool m_loadFailed;

        private int count = 0;

        private HashSet<IMyEntity> m_entitiesCache = new HashSet<IMyEntity>();
        public List<EnemyShip> m_deleteEnemyCache = new List<EnemyShip>();

        public override void UpdateBeforeSimulation()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            if (MyAPIGateway.Session == null)
                return;

            if (!m_loaded)
                Init();
            if (!m_loaded)
                return;

            count++;
            
            if (count % 30 == 0)
                UpdateEnemies();

            foreach (var enemyShip in m_data.EnemyShips)
                enemyShip.Update(count);
        }

        private void Init()
        {
            //try
            //{
                Logger = new MyLogger("Log.log");

                m_data = new MissionData();

                InitEnemies();

                m_loaded = true;
            //}
            //catch (NullReferenceException e)
            //{
            //    MyAPIGateway.Utilities.ShowNotification("Mission load failed, restart or re-download the files.", 30000, Sandbox.Common.MyFontEnum.Red);
            //    if (Logger != null)
            //    {
            //        Logger.WriteLine(e.Message);
            //        Logger.WriteLine(e.StackTrace);
            //        MyAPIGateway.Utilities.ShowNotification("Mission load failed, restart or re-download the files.", 30000, Sandbox.Common.MyFontEnum.Red);
            //    }
            //    m_loadFailed = true;
            //}
        }

        private void UpdateEnemies()
        {
            Boolean enemyCountChanged = false;
            Boolean killed = false;

            //Find npc player that was created in world in prior
            //var enemy = MyAPIGateway.Players.AllPlayers.Where((x) => x.Value.DisplayName == InvaderIdentifier).FirstOrDefault();
            //Logger.WriteLine("enemy " + enemy.Key);

            //long enemyKey = 0;
            //var players = MyAPIGateway.Players.AllPlayers;
            //foreach (var p in players)
            //{
            //    if (p.Value.DisplayName == Identifier)
            //    {
            //        enemyKey = p.Key;
            //        break;
            //    }
            //}

            MyAPIGateway.Entities.GetEntities(m_entitiesCache, (x) => x is IMyCubeGrid);
            Talk("System", m_entitiesCache.Count + " ships are picked up");
            foreach (var ent in m_entitiesCache)
            {
                if (!ent.DisplayName.Contains(Identifier))
                {
                    continue;
                }
                Talk("System", "a Invader is found");

                bool exists = false;
                foreach (var checkedShip in m_data.CheckedShips)
                {
                    if (checkedShip.Equals(ent))
                        exists = true;
                }
                if (!exists)
                {
                    Talk("System", "new Invader is found");

                    var tmpship = ent as IMyCubeGrid;
                    if (tmpship == null)
                    {
                        Talk("System", "[ERROR]cast failure");

                    }
                    else
                    {
                        EnemyShip tmpShipEnt = new EnemyShip(tmpship);
                        if (
                            tmpShipEnt.myShip == null
                            || tmpShipEnt.myShip.IsTrash()
                            || !tmpShipEnt.myShip.IsVisible()
                            || !tmpShipEnt.IsWorkingInvader()
                            )
                            break;

                        enemyCountChanged = true;
                        m_data.EnemyEncounted++;
                        MyAPIGateway.Utilities.ShowNotification(
                            "Enemy Incoming"
                            , 10000, Sandbox.Common.MyFontEnum.Red);
                        m_data.EnemyShips.Add(tmpShipEnt);

                        var grid = ent as IMyCubeGrid;
                        //grid.ChangeGridOwnership(enemy.Key, MyOwnershipShareModeEnum.None);
                        //grid.ChangeGridOwnership(enemyKey, MyOwnershipShareModeEnum.None);
                        m_data.CheckedShips.Add(ent as IMyCubeGrid);
                    }
                }
            }
            m_entitiesCache.Clear();

            foreach (var enemyShip in m_data.EnemyShips)
            {
                if (enemyShip.myShip == null
                    || enemyShip.myShip.IsTrash()
                    || !enemyShip.myShip.IsVisible()
                    || !enemyShip.IsWorkingInvader())
                {
                    Talk("System", "Signal Lost");

                    if (enemyShip.myShip.IsVisible() && !enemyShip.IsWorkingInvader())
                    {
                        Talk("System", "Enemy is Killed");

                        killed = true;
                        m_data.kill++;
                    }
                    m_deleteEnemyCache.Add(enemyShip);
                }
            }
            if (m_deleteEnemyCache.Count > 0)
            {
                enemyCountChanged = true;
                foreach (var enemyShip in m_deleteEnemyCache)
                    m_data.EnemyShips.Remove(enemyShip);
                    MyAPIGateway.Utilities.ShowNotification(
                        "Signal Lost"
                        , 10000, Sandbox.Common.MyFontEnum.Red);
                m_deleteEnemyCache.Clear();
            }

            if (enemyCountChanged)
            {
                MyAPIGateway.Utilities.ShowNotification(
                    "Enemy Exist:" + m_data.EnemyShips.Count + " / killed: " + m_data.kill
                    , 15000, Sandbox.Common.MyFontEnum.Red);

                if (m_data.kill % 100 == 0 && m_data.kill != 0)
                {
                    String msg = "You've got over " + m_data.kill + " Invaders!";
                    MyAPIGateway.Utilities.ShowNotification(
                        msg
                        , 30000, Sandbox.Common.MyFontEnum.Green);
                    Logger.WriteLine(msg);
                }

                if (killed)
                {
                    String title = "";
                    switch (m_data.kill)
                    {
                        case 4:
                            title = "You're Ace!";
                            break;
                        case 12:
                            title = "Got Dozen!";
                            break;
                        case 42:
                            title = "Here is the Answer to the Ultimate Question of Life, the Universe, and Everything.";
                            break;
                        case 153:
                            title = "It was full of large fish, 153, but even with so many the net was not torn.";
                            break;
                        case 230:
                            title = "The central creation myth is that an invisible and undetectable Flying Spaghetti Monster created the universe \"after drinking heavily\".";
                            break;
                    }
                    if (!title.Equals(""))
                    {
                        MyAPIGateway.Utilities.ShowNotification(
                            title
                            , 30000, Sandbox.Common.MyFontEnum.Green);
                        Logger.WriteLine(title);
                    }
                }
            }
        }

        private void InitEnemies()
        {
            Boolean enemyCountChanged = false;

            //long enemyKey = 0;
            //var players = MyAPIGateway.Players.AllPlayers;
            //foreach (var p in players)
            //{
            //    if (p.Value.DisplayName == Identifier)
            //    {
            //        enemyKey = p.Key;
            //        break;
            //    }
            //}
            //if (enemyKey == 0)
            //{
            //    enemyKey = MyAPIGateway.Players.AddNewNpc(Identifier);
            //    Logger.WriteLine("enemy " + enemyKey);
            //}

            MyAPIGateway.Entities.GetEntities(m_entitiesCache, (x) => x is IMyCubeGrid && x.DisplayName.Contains(Identifier));
            foreach (var ent in m_entitiesCache)
            {
                enemyCountChanged = true;
                m_data.EnemyEncounted++;
                MyAPIGateway.Utilities.ShowNotification(
                    "Enemy Incoming",
                    3000, Sandbox.Common.MyFontEnum.Red);

                m_data.EnemyShips.Add(new EnemyShip(ent as IMyCubeGrid));

                var grid = ent as IMyCubeGrid;
                //grid.ChangeGridOwnership(enemyKey, MyOwnershipShareModeEnum.None);

                m_data.CheckedShips.Add(ent as IMyCubeGrid);
            }
            m_entitiesCache.Clear();

            if (enemyCountChanged)
            {
                MyAPIGateway.Utilities.ShowNotification(
                    "Enemy Exist:" + m_data.EnemyShips.Count + " / Found:" + m_data.EnemyEncounted
                    , 10000, Sandbox.Common.MyFontEnum.Red);
            }
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            Logger.Close();
            Logger = null;
        }

        public static void Talk(String who, String what){
            if (!WantTalk)
                return;

            Logger.WriteLine(who + " said: " + what);
            MyAPIGateway.Utilities.ShowMessage(who, what);
        }
    }
}
