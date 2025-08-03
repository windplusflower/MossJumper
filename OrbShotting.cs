using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Modding;
using Satchel;
using UnityEngine;

namespace MossJumper;

public class OrbShotting : FsmStateAction {
    static public FsmGameObject orbTemplate;
    public GameObject parent;
    private FsmFloat times = 0f;

    public override void OnUpdate() {
        times.Value += Time.deltaTime;
        if (times.Value >= 0.1f) {
            times.Value = 0f;
            var orb = GameObject.Instantiate(orbTemplate.Value);
            orb.transform.position = parent.transform.position + new Vector3(5 * parent.transform.GetScaleX(), 0, 0);
            orb.GetComponent<Rigidbody2D>().velocity = new(1000 * parent.transform.GetScaleX(), 0);
            orb.LocateMyFSM("Orb Control").SendEvent("FIRE");
        }
    }
}