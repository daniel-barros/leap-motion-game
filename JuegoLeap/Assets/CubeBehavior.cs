//    Leap Motion Racing Game
//
// 	  Yoji Sargent Harada
//	  Daniel Barros López
//
//	  Last modified: Oct 30, 2015
//
//
//    Copyright (C) 2015  Yoji Sargent Harada, Daniel Barros López
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.


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
