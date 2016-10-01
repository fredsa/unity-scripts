using UnityEngine;
using System.Collections;
using System;

public class OverlaySphereController : MonoBehaviour
{
	Animator animator;

	void Awake ()
	{
		animator = GetComponent<Animator> ();
	}

	void OnEnable ()
	{
		GameMaster.instance.GameStateChange += OnGameStateChange;
	}

	void OnDisable ()
	{
		GameMaster.instance.GameStateChange -= OnGameStateChange;
	}

	void OnGameStateChange (GameState state)
	{
		switch (state) {
		case GameState.INITIALIZING_APP:
		case GameState.NEED_ACK_HEADPHONES:
		case GameState.LOBBY_SELECT_NUM_PLAYERS:
		case GameState.LOBBY_WAITING_FOR_OPPONENT:
		case GameState.GAME_WAS_TORN_DOWN:
		case GameState.PLAYING:
		case GameState.SELECTING_GAME_STYLE:
			if (animator.isInitialized) {
				animator.SetBool ("cloak", false);
			}
			break;
		case GameState.PLACING_SHIPS:
			animator.SetBool ("cloak", true);
			break;
		default:
			throw new NotImplementedException ();
		}
	}

}
