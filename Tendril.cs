using System.Collections;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Modding;
using Satchel;
using UnityEngine;

namespace MossJumper;

public class Tendril : FsmStateAction {
    static public FsmGameObject T, T2, T3;
    public Transform transform;
    public Vector3 delt = new Vector3(0, 0, 0);
    public float waitTimes = 0.5f;

    public override void OnEnter() {

        StartCoroutine(doTendril());
    }
    private IEnumerator doTendril() {
        yield return new WaitForSeconds(waitTimes);
        var t = GameObject.Instantiate(T.Value);
        var t2 = GameObject.Instantiate(T2.Value);
        var t3 = GameObject.Instantiate(T3.Value);
        var position = transform.position;
        t.transform.position = new Vector3(position.x, 6, 0) + delt;
        t2.transform.position = new Vector3(position.x + 1, 10, 0) + delt;
        t3.transform.position = new Vector3(position.x + 1, 10, 0) + delt;
        t.transform.eulerAngles = new Vector3(0, 0, 90);
        t2.transform.eulerAngles = new Vector3(0, 0, 90);
        t3.transform.eulerAngles = new Vector3(0, 0, 90);
        t.SetActive(true);
        t.GetComponent<PolygonCollider2D>().enabled = true;
        t2.SetActive(true);
        t3.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        t.SetActive(false);
        t2.SetActive(false);
        t3.SetActive(false);
        yield return null;
    }
}