//    Leap Motion Racing Game
//
// 	  Yoji Sargent Harada
//	  Daniel Barros López
//
//	  Last modified: Oct 29, 2015
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
using Leap;

enum GameState {
	NoHand, Menu, SettingUp, PreGestureTutorial, GestureTutorial, Playing
}

// This class handles the game Logic.
// The script should be attached to a persistent GameObject, like the main camera
public class MainBehavior : MonoBehaviour {
	
	Controller controller;
	GameState gameState;
	Leap.Vector initialHandPosition = new Leap.Vector(0, 160, 160);
	float positionErrorMargin = 30f;
	float errorMargin = 0.2f;
    const float initialCubeSpeed = 90;
	float cubeSpeed = initialCubeSpeed;
    float cubeAcceleration = 500;
    float initialCubeZPosition = 400;
	float minCubeZPosition = -60;
	Quaternion initialGuidingHandRotation;
	bool didShowPreTutorialMessage = false;
	int score = 0;
	float timeScale = 1;
	bool slowMotionActive = false;
	float slowMotionTimeScaleFactor = 1.0f/6.0f;
	int collisionPenalty = 20;
	int slowMotionPenalty = 100;
    bool shouldHideUI = false;
    bool gameHasStarted = false;


	// Time measurement
	float lastMeasuredTime = 0;
	float timeAtLastFrame;
	float timeSinceLastFrame = 0;
	float timeForMessageRemoval = -1;
	float timeSinceLastCubeCreation = 0;
	float cubeSpawningTime;
	float initialCubeSpawningTime = 0.5f;
	float preGestureTutorialStartTime = -1;
	float waitingEndTime = -1;

	float lastTimePalmNormalPointedDown = -1;
	float maxGestureDuration = 0.5f;
	float lastTimeGestureWasDetected = -1;
	float slowMotionDuration = 1;
	float preGestureTutorialDuration = 9;

	
	// IDE objects
	public HandController handController;
	public GameObject cubeTemplate;
	public GameObject blueCubeTemplate;
	public UnityEngine.UI.Text message;
    public UnityEngine.UI.Text menuText;
    public UnityEngine.UI.Text scoreText;
	public UnityEngine.UI.Text errorMarginText;
    public GameObject continueButton;
    public GameObject resetButton;
    public GameObject startButton;
	ArrayList cubes = new ArrayList ();
	public GameObject guidingHand;
    public GameObject road;
    public GameObject difficultySlider;
    public GameObject difficulty3DText;


	void Start () {
		controller = new Controller();
		timeAtLastFrame = Time.time;
		gameState = GameState.NoHand;
		initialGuidingHandRotation = guidingHand.transform.localRotation;
		cubeSpawningTime = initialCubeSpawningTime;
		errorMarginText.text = errorMargin.ToString ();
        resetGame();
        showMessage("");
    }
	
	void Update () {
		Frame frame = controller.Frame();
		Hand hand = frame.Hands.Leftmost;

		updateTime ();
		updateTexts ();

		// Update error margin according to key press
		if (Input.GetKeyDown("up")) {
			errorMargin += 0.1f;
			if (errorMargin > 1) {
				errorMargin = 1;
			}
		} else if (Input.GetKeyDown("down")) {
			errorMargin -= 0.1f;
			if (errorMargin < 0) {
				errorMargin = 0;
			}
		}
		errorMarginText.text = errorMargin.ToString ();

		// Do nothing until wait ends
		if (Time.time < waitingEndTime) {
			return;
		}

		// GAME PHASES

		// No hand or multiple hands detected
		if (frame.Hands.Count != 1) {
			gameState = GameState.NoHand;
			guidingHand.SetActive(false);
            setMenuHidden(false);
            menuText.text = "Coloca una sola mano cerca de tu dispositivo Leap Motion para empezar";
			return;
		} else if (gameState == GameState.NoHand) {
			gameState = GameState.Menu;
            setMenuHidden(false);
        }
        menuText.text = "";

        if (gameState == GameState.Menu) {
            handleMenuPhase();
        }

		// Placing hand at initial position
		if (gameState == GameState.SettingUp) {
            gameHasStarted = true;
			if (handIsInInitialPosition (hand)) {
				showMessage ("");
				guidingHand.SetActive (false);
				gameState = GameState.PreGestureTutorial;
				preGestureTutorialStartTime = Time.time;
			}
		}

		// Play for a few seconds before gesture tutorial starts
		if (gameState == GameState.PreGestureTutorial) {
			spawnCubes();
			moveCubes();
			updateScore ();

			if (!didShowPreTutorialMessage) {
				showMessage("Esquiva los cubos blancos para no perder puntos. " +
					"Intenta coger los cubos azules", preGestureTutorialDuration/2 - 1);
				didShowPreTutorialMessage = true;
			} else if (message.text.Equals("")) {
				showMessage("Para parar el juego, simplemente baja tu mano.", preGestureTutorialDuration/2 - 1);
			}
			if (Time.time - preGestureTutorialStartTime > preGestureTutorialDuration) {
				gameState = GameState.GestureTutorial;
			}
		}

		// Gesture tutorial shows how to activate slow motion
		if (gameState == GameState.GestureTutorial) {
			if (message.text == "") {
				showMessage("Para realentizar el tiempo realiza el gesto de parada. Gastarás "
				            + slowMotionPenalty.ToString() + " puntos");
				wait(2.5f);
				return;
			}

			guidingHand.SetActive(true);
			rotateGuidingHand();
			if (guidingHand.transform.rotation.x <= -0.75) {
				guidingHand.SetActive(false);
				gameState = GameState.Playing;
				showMessage("");

				activateSlowMotion();
			}
		}

		// Playing
		if (gameState == GameState.Playing) {
            gameHasStarted = true;
            spawnCubes();
			moveCubes();
			updateScore ();
			updateDifficulty();

			// End slow motion if proper
			if (Time.time - lastTimeGestureWasDetected > slowMotionDuration && 
			    lastTimeGestureWasDetected != -1 && slowMotionActive) {
				timeScale /= 1.5f;
				Time.timeScale = timeScale;
				slowMotionActive = false;
			}

			if (slowMotionActive) {
				return;
			}

			// Gesture recognition
			if (handIsInInitialGesturePosition (hand)) {
				lastTimePalmNormalPointedDown = Time.time;
			}
			if (handIsInFinalGesturePosition (hand) && score >= 100) {
				score -= slowMotionPenalty;
				if (Time.time - lastTimePalmNormalPointedDown <= maxGestureDuration) {
					activateSlowMotion();
				}
				lastTimePalmNormalPointedDown = -1;
			}
		}
	}
    
    // Handles the Menu phase game logic
    void handleMenuPhase () {
        showMessage("");

        // Handles cases for both reset and start buttons (same tag)
        bool startButtonActivated = startButton.activeSelf;
        bool resetButtonActivated = resetButton.activeSelf;
        if (!gameHasStarted) {
            startButton.SetActive(true);
        } else {
            resetButton.SetActive(true);
        }
        ButtonDemoToggle UIButton = GameObject.FindWithTag("ResetButton").GetComponent<ButtonDemoToggle>();
        if (UIButton.ToggleState) {
            if (shouldHideUI) {
                shouldHideUI = false;
                resetGame();
                setMenuHidden(true);
                gameState = GameState.SettingUp;
                guidingHand.transform.localRotation = initialGuidingHandRotation;
                guidingHand.SetActive(true);
            } else {
                shouldHideUI = true;
                wait(0.5f);
            }
            if (!gameHasStarted) {
                startButton.SetActive(startButtonActivated);
            } else {
                resetButton.SetActive(resetButtonActivated);
            }
            return;
        }
        if (!gameHasStarted) {
            startButton.SetActive(startButtonActivated);
        } else {
            resetButton.SetActive(resetButtonActivated);
        }

        // Continue button handler
        bool continueButtonActivated = continueButton.activeSelf;
        if (gameHasStarted) {
            continueButton.SetActive(true);
            UIButton = GameObject.FindWithTag("ContinueButton").GetComponent<ButtonDemoToggle>();
            if (UIButton.ToggleState) {
                if (shouldHideUI) {
                    shouldHideUI = false;
                    setMenuHidden(true);
                    gameState = GameState.Playing;
                } else {
                    shouldHideUI = true;
                    wait(0.5f);
                }
                continueButton.SetActive(continueButtonActivated);
                return;
            }
            continueButton.SetActive(continueButtonActivated);
        }

        // Difficulty slider handler
        bool difficultySliderActivated = difficultySlider.activeSelf;
        difficultySlider.SetActive(true);
        SliderDemo UISlider = GameObject.FindWithTag("DifficultySlider").GetComponent<SliderDemo>();
        cubeAcceleration = UISlider.GetSliderFraction() * 2000;
        difficultySlider.SetActive(difficultySliderActivated);
    }

    // Hide/show menu and hide/show gameplay objects
    void setMenuHidden (bool hidden) {
        if (!gameHasStarted) {
            setButtonHidden(hidden, startButton, "ResetButton");

            continueButton.SetActive(false);
            resetButton.SetActive(false);
        } else {
            setButtonHidden(hidden, continueButton, "ContinueButton");
            setButtonHidden(hidden, resetButton, "ResetButton");

            startButton.SetActive(false);
        }

        setCubesHidden(!hidden);
        road.SetActive(hidden);
        difficultySlider.SetActive(!hidden);
        difficulty3DText.SetActive(!hidden);

        showMessage("");

        Time.timeScale = hidden ? timeScale : 1;
    }

    // Hides/shows a button
    void setButtonHidden (bool hidden, GameObject button, string tag) {
        button.SetActive(true);
        ButtonDemoToggle UIButton = GameObject.FindWithTag(tag).GetComponent<ButtonDemoToggle>();
        UIButton.ToggleState = false;
        UIButton.ButtonTurnsOff();
        button.SetActive(!hidden);
    }

	// Keeps track of time and frames
	void updateTime () {
		timeSinceLastFrame = Time.time - timeAtLastFrame;
		timeAtLastFrame = Time.time;
	}

	// Updates texts in game UI
	void updateTexts () {
		// Message text
		if (timeForMessageRemoval != -1 && timeForMessageRemoval <= Time.time) {
			message.text = "";
			timeForMessageRemoval = -1;
		}
		// Score text
		scoreText.text = score.ToString () + " puntos";
	}

	// Slows down the cubes speed
	void activateSlowMotion () {
		slowMotionActive = true;
		Time.timeScale *= slowMotionTimeScaleFactor;
		lastTimeGestureWasDetected = Time.time;
	}

	// Creates a new cube if enough time has passed
	void spawnCubes() {
		if (Time.time - timeSinceLastCubeCreation > cubeSpawningTime) {
			timeSinceLastCubeCreation = Time.time;
			
			System.Random rnd = new System.Random();
			Vector3 cubePosition = new Vector3(rnd.Next(-40, 40), rnd.Next(30, 50), initialCubeZPosition);

			GameObject template = cubeTemplate;
			if (rnd.Next(0,10) == 0) {
				template = blueCubeTemplate;
			}
			Object cube = Instantiate(template, cubePosition, Quaternion.identity);
			cubes.Add(cube);
		}
	}

	// Resets game variables to initial values
	void resetGame() {
        cubeSpeed = initialCubeSpeed;
		timeScale = 1;
		Time.timeScale = timeScale;
		score = 0;
		cubeSpawningTime = initialCubeSpawningTime;
		slowMotionActive = false;

        foreach (GameObject cube in cubes)
        {
            DestroyObject(cube);
        }
        cubes.Clear();
    }

    // Hides cubes
    void setCubesHidden(bool hidden) {
        foreach (GameObject cube in cubes) {
            cube.SetActive(!hidden);
        }
    }

	// Moves all cubes forward, destroys those which are no longer visible
	void moveCubes() {
		ArrayList objectsToDelete = new ArrayList ();

		foreach (GameObject cube in cubes) {
			float z = cube.transform.position.z;
			z -= cubeSpeed * timeSinceLastFrame;
			cube.transform.position = new Vector3(cube.transform.position.x, cube.transform.position.y, z);

			if (z < minCubeZPosition) {
				objectsToDelete.Add(cube);
			}
		}
		foreach (GameObject cube in objectsToDelete) {
			cubes.Remove(cube);
			DestroyObject(cube);
		}
	}

	// Updates score each 0.5 seconds
	void updateScore() {
		// if 0.5 seconds passed (taking into account slow motion)
		if ((slowMotionActive && Time.time - lastMeasuredTime >= 0.5 * Time.timeScale) ||
		    (!slowMotionActive && Time.time - lastMeasuredTime >= 0.5)) {
			lastMeasuredTime = Time.time;
			score += 5;
		}
	}

	// Increases speed every 0.5 seconds if slow motion is not active
	void updateDifficulty() {
		if (slowMotionActive) {
			return;
		}
		if (Time.time == lastMeasuredTime) {	// Just updated lastMeasuredTime, 0.5 sec passed
            cubeSpeed += cubeAcceleration / cubeSpeed;
		}
	}
	
	// Compares two positions using an error margin
	bool positionsAreEqualWithinError(Leap.Vector pos1, Leap.Vector pos2, float error) {
		return Mathf.Abs(pos1.x - pos2.x) < error &&
			   Mathf.Abs(pos1.y - pos2.y) < error &&
			   Mathf.Abs(pos1.z - pos2.z) < error;
	}

	// Checks if the hand is at the initial position before game starts. Shows help messages if not.
	bool handIsInInitialPosition(Hand hand) {
		// Hand position
		if (!positionsAreEqualWithinError (hand.PalmPosition, initialHandPosition, positionErrorMargin)) {
			showMessage ("Ahora coloca tu mano horizontalmente en la posición indicada");
			guidingHand.SetActive(true);
			return false;
		}
		guidingHand.SetActive (false);

		// Palm normal down?
		if (Vector.Down.Dot (hand.PalmNormal) < 1 - errorMargin) {
			showMessage ("Coloca la palma de tu mano horizontalmente mirando hacia abajo");
			return false;
		}
		// Fingers direction forward?
		foreach (Finger finger in hand.Fingers) {
			if (finger.Type == Finger.FingerType.TYPE_THUMB) {
				continue;
			}
			if (Vector.Forward.Dot (finger.Direction) < 1 - errorMargin) {
				showMessage ("Mantén extendidos tus dedos");
				return false;
			}
		}
		return true;
	}

	// Checks if hand is at stop gesture's initial position
	bool handIsInInitialGesturePosition (Hand hand) {
		return Vector.Down.Dot (hand.PalmNormal) > 1 - errorMargin;
	}

	// Checks if hand is at stop gesture's final position
	bool handIsInFinalGesturePosition (Hand hand) {

		// Palm normal forward?
		if (Vector.Forward.Dot (hand.PalmNormal) < 1 - errorMargin) {
			return false;
		}
		// Fingers direction up?
		foreach (Finger finger in hand.Fingers) {
			if (finger.Type == Finger.FingerType.TYPE_THUMB) {
				continue;
			}
			if (Vector.Up.Dot (finger.Direction) < 1 - errorMargin - 0.2) {
				return false;
			}
		}
		return true;
	}

	// Rotates guiding hand imitating the stop gesture
	void rotateGuidingHand() {
		guidingHand.transform.RotateAround (guidingHand.transform.position, Vector3.right, -2);
	}

	// Game does nothing for x seconds. The variable waitingEndTime has to be checked in Update()
	void wait(float seconds) {
		waitingEndTime = Time.time + seconds;
	}

	// Shows up a message in the game UI, with an optional duration
	void showMessage(string text, float duration = 0) {
		if (message.text.Equals(text)) {
			return;
		}
		message.text = text;
		if (duration <= 0) {
			timeForMessageRemoval = -1;
		} else {
			timeForMessageRemoval = Time.time + duration;
		}
	}

	// Removes the cube that caused the collision and updates score
	// This method is called by the cube itself
	public void didCollideWithCube(Object cube) {
		cubes.Remove (cube);
		Destroy (cube);
		if (((GameObject)cube).CompareTag ("BlueCube")) {	// Blue cubes increase score
			score += collisionPenalty;
		} else {	// Regular cubes decrease score
			score -= collisionPenalty;
		}
	}

	Vector3 unityVector(Leap.Vector v) {
		return new Vector3(v.x, v.y, v.z);
	}
}
