/*
 * 空洞骑士Mod入门到进阶指南/配套模版
 * 作者：近环（https://space.bilibili.com/1224243724）
 */

using System.Runtime.ConstrainedExecution;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using JetBrains.Annotations;
using Modding;
using Satchel;
using Satchel.Futils;
using UnityEngine;

namespace MossJumper;

// Mod配置类，目前只有开关的配置。可以自行添加额外选项，并在GetMenuData里添加交互。
[Serializable]
public class Settings {
    public bool on = true;
}

public class MossJumper : Mod, IGlobalSettings<Settings>, IMenuMod {
    public static MossJumper instance;
    private static float distance = 3.77f;
    private static float leftWall = 32f;
    private static float rightWall = 69f;
    private static float floor = 8f;
    private GameObject orbTemplate;
    private GameObject nailTemplate;

    private Dictionary<string, int> skillWeights = new Dictionary<string, int>();

    private SkillSelector selector;
    /*  
     * ******** Mod名字和版本号 ********
     */
    public MossJumper() : base("MossJumper") {
        instance = this;
    }
    public override string GetVersion() => "1.0";

    /* 
     * ******** 预加载和hook ********
     */
    public override List<(string, string)> GetPreloadNames() {
        // 预加载你想要的攻击特效或者敌人，具体请阅读教程。
        return new List<(string, string)>
        {
            ("GG_Radiance", "Boss Control/Absolute Radiance"),
            ("GG_Hollow_Knight", "Battle Scene/HK Prime"),
        };
    }
    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
        // 添加需要使用的hooks
        On.PlayMakerFSM.OnEnable += PlayMakerFSM_OnEnable;
        var radiance = preloadedObjects["GG_Radiance"]["Boss Control/Absolute Radiance"];
        var radianceFSM = radiance.LocateMyFSM("Attack Commands");
        orbTemplate = radianceFSM.GetAction<SpawnObjectFromGlobalPool>("Spawn Fireball", 1).gameObject.Value;
        GameObject.Destroy(orbTemplate.GetComponent<PersistentBoolItem>());
        GameObject.Destroy(orbTemplate.GetComponent<ConstrainPosition>());
        OrbShotting.orbTemplate = orbTemplate;

        var nailComb = radianceFSM.GetAction<SpawnObjectFromGlobalPool>("Comb Top", 0).gameObject.Value;
        var nailCombFSM = nailComb.LocateMyFSM("Control");
        nailTemplate = nailCombFSM.GetAction<SpawnObjectFromGlobalPool>("RG1", 1).gameObject.Value;
        GameObject.Destroy(nailTemplate.GetComponent<PersistentBoolItem>());
        GameObject.Destroy(nailTemplate.GetComponent<ConstrainPosition>());
        NailShotting.nailTemplate = nailTemplate;

        var hkprime = preloadedObjects["GG_Hollow_Knight"]["Battle Scene/HK Prime"];
        var tenderil = hkprime.FindGameObjectInChildren("Tendrils");
        Tendril.T = tenderil.FindGameObjectInChildren("T Hit");
        Tendril.T2 = tenderil.FindGameObjectInChildren("T2");
        Tendril.T3 = tenderil.FindGameObjectInChildren("T3");

        ModHooks.LanguageGetHook += changeName;
    }
    private string changeName(string key, string title, string orig) {
        if (key == "MEGA_MOSS_MAIN" || key == "NAME_MEGA_MOSS_CHARGER" && mySettings.on) {
            return "大型苔藓冲飞者";
        }
        return orig;
    }

    /* 
     * ******** FSM相关改动，这个示例改动使得左特随机在空中多次假动作 ********
     */
    [Obsolete]
    private void PlayMakerFSM_OnEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
        if (mySettings.on) {
            if (self.gameObject.scene.name == "GG_Mega_Moss_Charger" && self.gameObject.name == "Mega Moss Charger" && self.FsmName == "Mossy Control") {
                Log("Updating Mossy Control FSM.");
                UpdateFsm(self);
                Prepare(self);
                ChooseDirection(self);
                ChooseJump(self);
                UpdateLeft(self);
                UpdateRight(self);
                UpdateJumpHigh(self);
                UpdateJumpLong(self);
                UpdateJumpWall(self);
                UpdateJumpRise(self);
                UpdateJumpDrop(self);
                UpdateLand(self);
                Accelerate(self);
                Debug(self);
            }
        }
        orig(self);
    }
    private void AddSkill(PlayMakerFSM fsm, string skillName, int weights) {
        fsm.CopyState("Leap Start", "Leap Start " + skillName);
        fsm.CopyState("Leap Launch", "Leap Launch " + skillName);
        fsm.CopyState("In Air", "In Air " + skillName);

        fsm.AddTransition("Choose Jump", skillName.ToUpper(), "Leap Start " + skillName);
        fsm.ChangeTransition("Leap Start " + skillName, "FINISHED", "Leap Launch " + skillName);
        fsm.ChangeTransition("Leap Launch " + skillName, "FINISHED", "In Air " + skillName);
        fsm.ChangeTransition("In Air " + skillName, "LAND", "Land");

        skillWeights.Add(skillName.ToUpper(), weights);
    }
    private void UpdateFsm(PlayMakerFSM fsm) {
        Log("Updating Mossy Charger FSM.");
        skillWeights = new Dictionary<string, int>();
        fsm.AddState("Choose Jump");
        fsm.ChangeTransition("Music 2", "FINISHED", "Choose Jump");
        fsm.AddState("Super Land");
        fsm.AddTransition("Land", "SUPER", "Super Land");
        fsm.AddTransition("Super Land", "FINISHED", "Submerge 1");

        AddSkill(fsm, "High", 10);
        AddSkill(fsm, "Long", 10);
        AddSkill(fsm, "Wall", 10);
        AddSkill(fsm, "Rise", 15);
        AddSkill(fsm, "Drop", 15);

        fsm.AddVariable<FsmFloat>("Moss Direction");
        fsm.AddVariable<FsmFloat>("Current Rise Speed");
        fsm.AddVariable<FsmFloat>("Backup Start Y");

        selector = new SkillSelector(skillWeights, 5);
    }
    private void Prepare(PlayMakerFSM fsm) {
        Log("Preparing Mossy Charger FSM.");
        fsm.GetValidState("Roar").InsertCustomAction(() => {
            fsm.FsmVariables.FindFsmFloat("Backup Start Y").Value = fsm.FsmVariables.FindFsmFloat("Start Y").Value;
        }, 0);
    }
    private void ChooseDirection(PlayMakerFSM fsm) {
        Log("Choosing direction for Mossy Charger.");
        // 从小骑士面朝着的方向钻出
        var choice = fsm.GetValidState("Left or Right?");
        choice.RemoveAction(0);
        choice.InsertCustomAction(() => {
            var Knight = GameObject.Find("Knight");
            int direction = Knight.transform.GetScaleX() < 0 ? 1 : -1;
            fsm.FsmVariables.FindFsmFloat("Moss Direction").Value = direction;
            fsm.FsmVariables.FindFsmFloat("Start Y").Value = fsm.FsmVariables.FindFsmFloat("Backup Start Y").Value;
            Log("set direction: " + direction);
            // 朝右
            if (direction == 1) {
                fsm.FsmVariables.FindFsmFloat("Current Charge Speed").Value = -1;
                Log("right charge speed: " + fsm.FsmVariables.FindFsmFloat("Current Charge Speed").Value);
                fsm.SendEvent("RIGHT");
            }
            else {
                fsm.FsmVariables.FindFsmFloat("Current Charge Speed").Value = 1;
                Log("left charge speed: " + fsm.FsmVariables.FindFsmFloat("Current Charge Speed").Value);
                fsm.SendEvent("LEFT");
            }
        }, 0);
    }
    private void ChooseJump(PlayMakerFSM fsm) {
        Log("Choosing jump for Mossy Charger.");
        // 选择跳跃方式
        var choice = fsm.GetValidState("Choose Jump");
        choice.InsertCustomAction(() => {
            var a = selector.GetRandomSkill();
            Log("Chosen skill: " + a);
            fsm.SendEvent(a);
        }, 0);
    }
    private void UpdateLeft(PlayMakerFSM fsm) {
        Log("Updating Left or Right choice for Mossy Charger.");
        fsm.GetAction<FloatCompare>("Emerge Left", 8).lessThan = FsmEvent.Finished;
        fsm.GetValidState("Emerge Left").RemoveAction(7);
        fsm.GetValidState("Emerge Left").RemoveAction(5);
        fsm.GetValidState("Emerge Left").RemoveAction(3);
    }
    private void UpdateRight(PlayMakerFSM fsm) {
        Log("Updating Right or Left choice for Mossy Charger.");
        fsm.GetAction<FloatCompare>("Emerge Right", 9).greaterThan = FsmEvent.Finished;
        fsm.GetValidState("Emerge Right").RemoveAction(8);
        fsm.GetValidState("Emerge Right").RemoveAction(7);
        fsm.GetValidState("Emerge Right").RemoveAction(5);
        fsm.GetValidState("Emerge Right").RemoveAction(3);
    }
    private void UpdateJumpHigh(PlayMakerFSM fsm) {
        Log("Updating Mossy Charger jump.");

        var start = fsm.GetValidState("Leap Start High");
        start.RemoveAction(5);
        start.InsertCustomAction(() => {
            Log("Setting position for Mossy Charger high jump.");
            var Knight = GameObject.Find("Knight");
            if (Knight != null) {
                var knightPos = Knight.transform.position;
                var direction = fsm.FsmVariables.FindFsmFloat("Moss Direction").Value;
                fsm.transform.position = new Vector3(knightPos.x + distance * direction, fsm.FsmVariables.FindFsmFloat("Start Y").Value + 0.1f, 0);
                fsm.FsmVariables.FindFsmFloat("Current Rise Speed").Value = 50f;
            }
        }, 5);

        var launch = fsm.GetValidState("Leap Launch High");
        fsm.GetAction<SetVelocity2d>("Leap Launch High", 3).y = fsm.GetVariable<FsmFloat>("Current Rise Speed");
        launch.InsertAction(new FloatMultiply {
            floatVariable = fsm.FsmVariables.FindFsmFloat("Current Charge Speed"),
            multiplyBy = 7.5f
        }, 0);

        var air = fsm.GetValidState("In Air High");
        air.AddAction(new OrbShotting {
            parent = fsm.gameObject
        });
    }
    private void UpdateJumpLong(PlayMakerFSM fsm) {
        Log("Updating Mossy Charger long jump.");

        var start = fsm.GetValidState("Leap Start Long");

        start.RemoveAction(5);
        start.InsertCustomAction(() => {
            Log("Setting position for Mossy Charger long jump.");
            var Knight = GameObject.Find("Knight");
            if (Knight != null) {
                var mid = (leftWall + rightWall) / 2;
                var dis = (rightWall - leftWall) / 2;
                var direction = fsm.FsmVariables.FindFsmFloat("Moss Direction").Value;

                fsm.transform.position = new Vector3(mid + dis * direction, floor + 2f, 0);

                fsm.FsmVariables.FindFsmFloat("Current Rise Speed").Value = 20f;
            }
        }, 5);

        var launch = fsm.GetValidState("Leap Launch Long");
        fsm.GetAction<SetVelocity2d>("Leap Launch Long", 3).y = fsm.GetVariable<FsmFloat>("Current Rise Speed");
        launch.InsertAction(new FloatMultiply {
            floatVariable = fsm.FsmVariables.FindFsmFloat("Current Charge Speed"),
            multiplyBy = 50f
        }, 0);

        var air = fsm.GetValidState("In Air Long");
        air.AddAction(new NailShotting {
            parent = fsm.gameObject,
            speed = 50,
            direction = 1
        });
    }
    private void UpdateJumpWall(PlayMakerFSM fsm) {
        Log("Updating Mossy Charger wall jump.");

        var start = fsm.GetValidState("Leap Start Wall");
        start.RemoveAction(5);
        start.InsertCustomAction(() => {
            Log("Setting position for Mossy Charger wall jump.");
            var Knight = GameObject.Find("Knight");
            if (Knight != null) {
                var mid = (leftWall + rightWall) / 2;
                var dis = (rightWall - leftWall) / 2;
                var direction = fsm.FsmVariables.FindFsmFloat("Moss Direction").Value;
                fsm.transform.position = new Vector3(mid + dis * direction, floor + 2f, 0);
                fsm.FsmVariables.FindFsmFloat("Current Rise Speed").Value = 20f;
            }
        }, 5);

        var launch = fsm.GetValidState("Leap Launch Wall");
        fsm.GetAction<SetVelocity2d>("Leap Launch Wall", 3).y = fsm.GetVariable<FsmFloat>("Current Rise Speed");
        launch.InsertAction(new FloatMultiply {
            floatVariable = fsm.FsmVariables.FindFsmFloat("Current Charge Speed"),
            multiplyBy = 0f
        }, 0);

        var air = fsm.GetValidState("In Air Wall");
        air.AddAction(new NailShotting {
            parent = fsm.gameObject,
            speed = 50,
            direction = 0
        });
    }
    private void UpdateJumpRise(PlayMakerFSM fsm) {
        Log("Updating Mossy Charger rise jump.");

        var start = fsm.GetValidState("Leap Start Rise");
        start.RemoveAction(5);
        start.InsertCustomAction(() => {
            Log("Setting position for Mossy Charger rise jump.");
            var Knight = GameObject.Find("Knight");
            if (Knight != null) {
                var knightPos = Knight.transform.position;
                var direction = fsm.FsmVariables.FindFsmFloat("Moss Direction").Value;
                fsm.transform.position = new Vector3(knightPos.x + distance * direction, floor - 1, 0);
                fsm.FsmVariables.FindFsmFloat("Current Rise Speed").Value = 20f;
                fsm.FsmVariables.FindFsmFloat("Start Y").Value = 20;
            }
        }, 5);
        start.InsertCustomAction(() => {
            fsm.transform.eulerAngles = new Vector3(0, 0, 90 * fsm.transform.GetScaleX());
        }, 0);

        var launch = fsm.GetValidState("Leap Launch Rise");
        fsm.GetAction<SetVelocity2d>("Leap Launch Rise", 3).y = fsm.GetVariable<FsmFloat>("Current Rise Speed");
        fsm.GetAction<SetGravity2dScale>("Leap Launch Rise", 4).gravityScale = 0.0f;
        launch.InsertAction(new FloatMultiply {
            floatVariable = fsm.FsmVariables.FindFsmFloat("Current Charge Speed"),
            multiplyBy = 0f
        }, 0);

        var air = fsm.GetValidState("In Air Rise");
        for (int i = 0; i < 4; i++) {
            air.AddAction(new NailShotting {
                parent = fsm.gameObject,
                direction = 2,
                speed = 10,
                timeInterval = 0.2f,
                positionV = new Vector3(-1.5f + i, -1f, 0f),
                times = UnityEngine.Random.Range(0f, 0.2f)
            });
        }
        fsm.GetAction<FloatCompare>("In Air Rise", 1).lessThan = null;
        fsm.GetAction<FloatCompare>("In Air Rise", 1).greaterThan = FsmEvent.FindEvent("LAND");
    }
    private void UpdateJumpDrop(PlayMakerFSM fsm) {
        Log("Updating Mossy Charger drop jump.");

        var start = fsm.GetValidState("Leap Start Drop");
        start.RemoveAction(5);
        start.InsertCustomAction(() => {
            Log("Setting position for Mossy Charger drop jump.");
            var Knight = GameObject.Find("Knight");
            if (Knight != null) {
                var knightPos = Knight.transform.position;
                var direction = fsm.FsmVariables.FindFsmFloat("Moss Direction").Value;
                fsm.transform.position = new Vector3(knightPos.x + distance * direction * 1.3f, 20f, 0);
                fsm.FsmVariables.FindFsmFloat("Current Rise Speed").Value = -10f;
                fsm.gameObject.FindGameObjectInChildren("Leap Hit").transform.localScale = new Vector3(0.5f, 0.5f, 1);
            }
        }, 5);
        start.InsertCustomAction(() => {
            fsm.transform.eulerAngles = new Vector3(0, 0, -90 * fsm.transform.GetScaleX());
        }, 0);

        var launch = fsm.GetValidState("Leap Launch Drop");
        fsm.GetAction<SetVelocity2d>("Leap Launch Drop", 3).y = fsm.GetVariable<FsmFloat>("Current Rise Speed").Value;
        launch.InsertAction(new FloatMultiply {
            floatVariable = fsm.FsmVariables.FindFsmFloat("Current Charge Speed"),
            multiplyBy = 0f
        }, 0);

        var air = fsm.GetValidState("In Air Drop");
        air.AddAction(new Tendril {
            transform = fsm.transform,
        });
    }
    private void UpdateLand(PlayMakerFSM fsm) {
        fsm.GetValidState("Land").AddCustomAction(() => {
            if (fsm.GetComponent<HealthManager>().hp < 180) {
                fsm.SendEvent("SUPER");
            }
        });
        var sland = fsm.GetValidState("Super Land");
        for (int i = 0; i < 8; i++) {
            sland.AddAction(new Tendril {
                transform = HeroController.instance.transform,
                delt = new Vector3(i * 6f - 22f, 0, 0),
                waitTimes = 0.1f
            });
        }
        sland.AddCustomAction(() => {
            fsm.SendEvent("FINISHED");
        });

    }
    private void Accelerate(PlayMakerFSM fsm) {
        Log("Accelerating Mossy Charger.");
        fsm.GetAction<Decelerate>("Submerge 1", 2).deceleration = 10f;
        fsm.GetAction<Decelerate>("Submerge 2", 0).deceleration = 10f;
        fsm.GetAction<Decelerate>("Submerge 3", 0).deceleration = 10f;
        fsm.GetAction<Decelerate>("Submerge 4", 0).deceleration = 10f;
        var ani = fsm.gameObject.GetComponent<tk2dSpriteAnimator>();
        ani.Library.GetClipByName("Disappear 1").fps = 300;
        ani.Library.GetClipByName("Disappear 2").fps = 50;
        ani.Library.GetClipByName("Disappear 3").fps = 50;
        ani.Library.GetClipByName("Disappear 4").fps = 300;

        fsm.RemoveAction("Submerge CD", 7);
        fsm.GetValidState("Submerge 1").InsertCustomAction(() => {
            fsm.transform.eulerAngles = new Vector3(0, 0, 0);
            fsm.gameObject.FindGameObjectInChildren("Leap Hit").transform.localScale = new Vector3(1f, 1f, 1);
        }, 0);
    }
    private void AddDebug(PlayMakerFSM fsm, string skillName) {
        fsm.GetValidState("Leap Start " + skillName).InsertCustomAction(() => {
            Log("Enter Leap Start " + skillName);
        }, 0);
        fsm.GetValidState("Leap Launch " + skillName).InsertCustomAction(() => {
            Log("Enter Leap Launch " + skillName);
        }, 0);
        fsm.GetValidState("In Air " + skillName).InsertCustomAction(() => {
            Log("Enter In Air " + skillName);
        }, 0);
    }
    private void Debug(PlayMakerFSM fsm) {
        fsm.GetValidState("Emerge Left").InsertCustomAction(() => {
            Log("Enter Emerge Left.");
        }, 0);
        fsm.GetValidState("Emerge Right").InsertCustomAction(() => {
            Log("Enter Emerge Right.");
        }, 0);
        fsm.GetValidState("Choose Jump").InsertCustomAction(() => {
            Log("Enter Choose Jump.");
        }, 0);
        AddDebug(fsm, "High");
        AddDebug(fsm, "Long");
        fsm.GetValidState("Land").InsertCustomAction(() => {
            Log("Enter Land.");
        }, 0);

        fsm.GetValidState("Leap Launch High").AddCustomAction(() => {
            var speed = fsm.FsmVariables.FindFsmFloat("Current Charge Speed").Value;
            Log("Current Charge Speed: " + speed);
        });
    }
    /* 
     * ******** 配置文件读取和菜单设置，如没有额外需求不需要改动 ********
     */
    private Settings mySettings = new();
    public bool ToggleButtonInsideMenu => true;
    // 读取配置文件
    public void OnLoadGlobal(Settings settings) => mySettings = settings;
    // 写入配置文件
    public Settings OnSaveGlobal() => mySettings;
    // 设置菜单格式
    public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? menu) {
        List<IMenuMod.MenuEntry> menus = new();
        menus.Add(
            new() {
                // 这是个单选菜单，这里提供开和关两种选择。
                Values = new string[]
                {
                    Language.Language.Get("MOH_ON", "MainMenu"),
                    Language.Language.Get("MOH_OFF", "MainMenu"),
                },
                // 把菜单的当前被选项更新到配置变量
                Saver = i => mySettings.on = i == 0,
                Loader = () => mySettings.on ? 0 : 1,
                Name = "Moss Jumper",
            }
        );
        return menus;
    }
}
