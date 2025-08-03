using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using IL.HutongGames.PlayMaker.Actions;
using Modding;
using Satchel;
using UnityEngine;

namespace MossJumper;

public class NailShotting : FsmStateAction {
    static public FsmGameObject nailTemplate;
    public GameObject parent;
    //0:前，1:后，2：上，3：下
    public int direction = 1;
    public int speed = 50;
    public Vector3 positionV = new Vector3(0, -1, 0);
    public float timeInterval = 0.05f;
    public FsmFloat times = 0f;

    public override void OnUpdate() {
        times.Value += Time.deltaTime;
        if (times.Value >= timeInterval) {
            times.Value = 0f;
            var nail = GameObject.Instantiate(nailTemplate.Value);
            var fsm = nail.LocateMyFSM("Control");
            updateFSM(fsm);
            fsm.SendEvent("SHOT");
        }
    }
    private void updateFSM(PlayMakerFSM fsm) {
        fsm.CopyState("Fan Antic", "Prepare");
        fsm.AddState("Prepare");
        fsm.AddState("Shoot");
        fsm.AddGlobalTransition("SHOT", "Prepare");
        fsm.ChangeTransition("Prepare", "FINISHED", "Shoot");
        fsm.AddTransition("Shoot", "TORECYCLE", "Recycle");

        updatePrepare(fsm);
        updateShoot(fsm);
    }
    private void updatePrepare(PlayMakerFSM fsm) {

        var pre = fsm.GetValidState("Prepare");
        pre.AddCustomAction(() => {
            fsm.gameObject.transform.position = parent.transform.position + positionV;
            switch (direction) {
                case 0:
                    fsm.gameObject.transform.eulerAngles = new Vector3(0, 0, 180 + 90 * parent.transform.GetScaleX());
                    fsm.gameObject.GetComponent<Rigidbody2D>().velocity = new Vector2(speed, 0) * parent.transform.GetScaleX();
                    break;
                case 1:
                    fsm.gameObject.transform.eulerAngles = new Vector3(0, 0, 180 - 90 * parent.transform.GetScaleX());
                    fsm.gameObject.GetComponent<Rigidbody2D>().velocity = new Vector2(-speed, 0) * parent.transform.GetScaleX();
                    break;
                case 2:
                    fsm.gameObject.transform.eulerAngles = new Vector3(0, 0, 180);
                    fsm.gameObject.GetComponent<Rigidbody2D>().velocity = new Vector2(0, -speed);
                    break;
                case 3:
                    fsm.gameObject.transform.eulerAngles = new Vector3(0, 0, 0);
                    fsm.gameObject.GetComponent<Rigidbody2D>().velocity = new Vector2(0, speed);
                    break;
            }
            fsm.GetComponent<PolygonCollider2D>().enabled = true;
        });
    }
    private void updateShoot(PlayMakerFSM fsm) {
        var shot = fsm.GetValidState("Shoot");
        var waitAction = new HutongGames.PlayMaker.Actions.Wait();
        waitAction.time = 2f;
        waitAction.finishEvent = FsmEvent.FindEvent("TORECYCLE");
        waitAction.realTime = false;
        shot.AddAction(waitAction);
    }
}