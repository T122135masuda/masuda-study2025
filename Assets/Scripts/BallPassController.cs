using System.Collections.Generic;
using UnityEngine;

public class BallPassController : MonoBehaviour
{
	public enum PassTeam
	{
		White,
		Black
	}

	[Header("Pass Settings")]
	[Tooltip("どのチーム同士でパスするか（White/Black）")]
	public PassTeam passTeam = PassTeam.White;
	[Tooltip("ボールの移動速度（m/s）")]
	public float passSpeed = 6.0f;
	[Tooltip("受け手に到達後に保持する時間（秒）")]
	public float holdTimeAtReceiver = 0.25f;
	[Tooltip("パス時の放物線の高さ（m）")]
	public float arcHeight = 0.7f;
	[Tooltip("ターゲットの高さ（胸の高さを想定）")]
	public float targetHeight = 1.2f;
	[Tooltip("最小パス距離（近すぎる相手はスキップ）")]
	public float minPassDistance = 0.5f;
	[Tooltip("ターゲット更新間隔（秒）")]
	public float refreshInterval = 0.2f;
	[Tooltip("自動開始（Play時に自動で開始）")]
	public bool autoStart = false;

	[Header("Debug")]
	public bool enableDebugLogs = false;
	[Tooltip("移動確認ログ（一定間隔で位置を出力）")]
	public bool logMovement = false;
	[Tooltip("移動ログの出力間隔（秒）")]
	public float movementLogInterval = 0.5f;

	private float _movementLogTimer = 0f;
	private Vector3 _lastLoggedPos;

	private Transform _ball; // シーンの ball オブジェクト
	private Rigidbody _rb;
	private List<Transform> _teammates = new List<Transform>();
	private int _currentIndex = 0;
	private bool _isMoving = false;
	private float _holdTimer = 0f;
	private float _refreshTimer = 0f;
	private Vector3 _startPos;
	private Vector3 _endPos;
	private float _travelDistance;
	private float _passT = 0f; // 0→1 の進捗
	private bool _waitingForStart = true; // 外部トリガー待機

	private void Start()
	{
		// ball を名前で取得（大文字小文字や部分一致にもある程度対応）
		var ballGo = GameObject.Find("ball");
		if (ballGo == null)
		{
			foreach (var t in FindObjectsOfType<Transform>())
			{
				if (t.name.Equals("ball", System.StringComparison.OrdinalIgnoreCase))
				{
					ballGo = t.gameObject;
					break;
				}
			}
		}
		if (ballGo == null)
		{
			Debug.LogWarning("BallPassController: 'ball' オブジェクトが見つかりません。");
			enabled = false;
			return;
		}
		_ball = ballGo.transform;
		_rb = ballGo.GetComponent<Rigidbody>();
		if (_rb != null)
		{
			// 物理で位置が書き戻されるのを防ぐ
			_rb.isKinematic = true;
			_rb.useGravity = false;
		}

		RefreshTeammates();
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: teammates={_teammates.Count}, team={passTeam}");
		}
		_waitingForStart = !autoStart;
		if (autoStart && _teammates.Count >= 2)
		{
			BeginNextPass();
		}
		else
		{
			// 待機中はチームの基準アンカー上にボールを固定
			SnapBallToTeamAnchor();
		}
	}

	private void Update()
	{
		_refreshTimer -= Time.deltaTime;
		if (_refreshTimer <= 0f)
		{
			RefreshTeammates();
			if (enableDebugLogs)
			{
				Debug.Log($"BallPassController: refreshed teammates={_teammates.Count}");
			}
			_refreshTimer = refreshInterval;
		}

		if (_ball == null || _teammates.Count < 2)
		{
			return;
		}

		// 自動開始のフォールバックは無効（開始は外部からの Resume タイミングに同期）

		if (_isMoving)
		{
			// 進捗ベースで移動（速度 → 時間あたりの進捗量に変換）
			float deltaT = _travelDistance > 0.0001f ? (passSpeed / _travelDistance) * Time.deltaTime : 1f * Time.deltaTime;
			_passT = Mathf.Min(1f, _passT + deltaT);
			Vector3 pos = Vector3.Lerp(_startPos, _endPos, _passT);
			float heightOffset = Mathf.Sin(_passT * Mathf.PI) * arcHeight;
			pos.y += heightOffset;
			MoveBall(pos);

			// 目標に到達したらホールドへ
			if (_passT >= 1f)
			{
				_isMoving = false;
				_holdTimer = holdTimeAtReceiver;
				if (enableDebugLogs)
				{
					Debug.Log($"BallPassController: arrived receiver index={_currentIndex}, pos={_ball.position}");
				}
			}
		}
		else
		{
			if (_waitingForStart)
			{
				// 外部開始を待機。位置はアンカーに固定
				SnapBallToTeamAnchor();
			}
			else
			{
				_holdTimer -= Time.deltaTime;
				if (_holdTimer <= 0f)
				{
					BeginNextPass();
				}
			}
		}

		// 移動確認ログ
		if (logMovement)
		{
			_movementLogTimer -= Time.deltaTime;
			if (_movementLogTimer <= 0f)
			{
				_movementLogTimer = Mathf.Max(0.05f, movementLogInterval);
				if (_ball != null)
				{
					if (enableDebugLogs)
					{
						Debug.Log($"BallPassController: moving={_isMoving}, pos={_ball.position}");
					}
				}
			}
		}
	}

	private void BeginNextPass()
	{
		if (_teammates.Count < 2)
		{
			if (enableDebugLogs)
			{
				Debug.LogWarning("BallPassController: teammates < 2 でパスできません。");
			}
			return;
		}
		int nextIndex = (_currentIndex + 1) % _teammates.Count;

		Transform from = _teammates[_currentIndex];
		Transform to = _teammates[nextIndex];

		Vector3 fromPos = GetTargetPosition(from);
		Vector3 toPos = GetTargetPosition(to);

		if (Vector3.Distance(fromPos, toPos) < minPassDistance)
		{
			_currentIndex = nextIndex;
			BeginNextPass();
			return;
		}

		// ボールを送出元にスナップ（開始が遠すぎる場合の保険）
		if (Vector3.Distance(_ball.position, fromPos) > 0.3f)
		{
			MoveBall(fromPos);
		}

		_startPos = fromPos;
		_endPos = toPos;
		_travelDistance = Vector3.Distance(_startPos, _endPos);
		_passT = 0f;
		_isMoving = true;
		_currentIndex = nextIndex;
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: pass {_startPos} -> {_endPos}, dist={_travelDistance}");
		}
	}

	private void MoveBall(Vector3 position)
	{
		if (_rb != null && !_rb.isKinematic)
		{
			// 念のため
			_rb.isKinematic = true;
			_rb.useGravity = false;
		}
		_ball.position = position;
	}

	// 手動開始用API
	public void StartPassingNow()
	{
		RefreshTeammates();
		_waitingForStart = false;
		_holdTimer = 0f;
		BeginNextPass();
	}

	private void SnapBallToTeamAnchor()
	{
		if (_ball == null) return;
		RefreshTeammates();
		Transform anchor = null;
		string anchorName = passTeam == PassTeam.White ? "capsule-w-1" : "capsule-b-1";
		for (int i = 0; i < _teammates.Count; i++)
		{
			if (_teammates[i].name.ToLower().Contains(anchorName))
			{
				anchor = _teammates[i];
				_currentIndex = i;
				break;
			}
		}
		if (anchor == null && _teammates.Count > 0)
		{
			anchor = _teammates[0];
			_currentIndex = 0;
		}
		if (anchor != null)
		{
			MoveBall(GetTargetPosition(anchor));
		}
	}

	private Vector3 GetTargetPosition(Transform agent)
	{
		Vector3 pos = agent.position;
		pos.y += targetHeight;
		return pos;
	}

	private void RefreshTeammates()
	{
		_teammates.Clear();
		if (CourtManager.Instance == null) return;
		foreach (var agent in CourtManager.Instance.agents)
		{
			if (agent == null) continue;
			string name = agent.gameObject.name.ToLower();
			bool isWhite = name.Contains("capsule-w");
			bool isBlack = name.Contains("capsule-b");
			if (passTeam == PassTeam.White && isWhite)
			{
				_teammates.Add(agent.transform);
			}
			else if (passTeam == PassTeam.Black && isBlack)
			{
				_teammates.Add(agent.transform);
			}
		}
		// インデックスの正規化
		if (_teammates.Count > 0)
		{
			_currentIndex = Mathf.Clamp(_currentIndex, 0, _teammates.Count - 1);
		}
	}

	// 外部からチームを切り替えるためのAPI
	public void SetPassTeam(PassTeam team)
	{
		passTeam = team;
		RefreshTeammates();
	}
}


