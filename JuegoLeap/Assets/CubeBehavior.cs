using UnityEngine;
using System.Collections;

// Listens for cube collisions and notifies the MainBehavior object
public class CubeBehavior : MonoBehaviour {

	public GameObject mainBehaviorObject;

	void OnCollisionEnter(Collision col) {
		MainBehavior script = (MainBehavior)mainBehaviorObject.GetComponent (typeof(MainBehavior));
		script.didCollideWithCube (gameObject);
	}
}
