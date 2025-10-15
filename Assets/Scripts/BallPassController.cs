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
	// 開始/待機カプセル番号は廃止（待機中は常に番号1へ固定）
	[Tooltip("予測位置計算を有効にする（無効にすると直線的なパスになる）")]
	public bool enablePrediction = false;
	[Tooltip("着地点の精度を向上させる（カプセルの中心を正確に計算）")]
	public bool enablePreciseLanding = true;
	[Tooltip("パス時の静止機能を有効にする")]
	public bool enablePassPause = true;
	[Tooltip("パス時の静止時間（秒）")]
	[Range(0.1f, 3.0f)]
	public float passPauseDuration = 2.5f;
	[Tooltip("ボールの移動速度（m/s）")]
	public float passSpeed = 7.0f;
	[Tooltip("受け手に到達後に保持する時間（秒）")]
	public float holdTimeAtReceiver = 0.4f;
	[Tooltip("パス時の放物線の最大高さ（m）— 実際は距離に応じて上限適用")]
	public float arcHeight = 0.35f;
	[Tooltip("ターゲットの高さ（胸の高さを想定）")]
	public float targetHeight = 1.2f;
	[Tooltip("最小パス距離（近すぎる相手はスキップ）")]
	public float minPassDistance = 0.5f;
	[Tooltip("ターゲット更新間隔（秒）")]
	public float refreshInterval = 0.1f;
	[Tooltip("自動開始（Play時に自動で開始）")]
	public bool autoStart = false;

	[Header("Speed Control")]
	[Tooltip("速度調整の有効/無効")]
	public bool enableSpeedControl = true;
	[Tooltip("最小速度（m/s）")]
	[Range(0.5f, 20f)]
	public float minSpeed = 0.5f;
	[Tooltip("最大速度（m/s）")]
	[Range(1f, 30f)]
	public float maxSpeed = 12.0f;
	[Tooltip("速度変化の加速度（m/s²）")]
	[Range(0.1f, 10f)]
	public float speedAcceleration = 2.0f;
	[Tooltip("現在の目標速度")]
	[Range(1f, 30f)]
	public float targetSpeed = 7.0f;
	[Tooltip("速度プリセット")]
	public SpeedPreset speedPreset = SpeedPreset.Fast;
	[Tooltip("キーボードで速度調整を有効にする")]
	public bool enableKeyboardControl = true;
	[Tooltip("キーボード速度調整の刻み値")]
	[Range(0.1f, 2f)]
	public float keyboardSpeedStep = 1.0f;

	public enum SpeedPreset
	{
		VerySlow = 0,
		Slow = 1,
		Normal = 2,
		Fast = 3,
		VeryFast = 4,
		Custom = 5
	}

	[Header("Pass Counter")]
	[Tooltip("パス回数カウント機能の有効/無効")]
	public bool enablePassCounter = true;
	[Tooltip("パス回数を画面に表示する")]
	public bool showPassCount = true;
	[Tooltip("パス回数表示位置（画面左上からのオフセット）")]
	public Vector2 passCountDisplayOffset = new Vector2(10, 220);
	[Tooltip("パス回数表示のサイズ")]
	public int passCountFontSize = 16;

	[Header("Debug")]
	public bool enableDebugLogs = false;
	[Tooltip("移動確認ログ（一定間隔で位置を出力）")]
	public bool logMovement = false;
	[Tooltip("移動ログの出力間隔（秒）")]
	public float movementLogInterval = 0.5f;
	[Tooltip("速度情報を画面に表示する")]
	public bool showSpeedInfo = true;

	private float _movementLogTimer = 0f;
	private Vector3 _lastLoggedPos;
	private float _actualSpeed = 0f; // 実測の現在速度（m/s）

	private Transform _ball; // シーンの Ball オブジェクト
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
																				// 初期（再生直後）に取得した各カプセルの位置を保存
	private readonly Dictionary<Transform, Vector3> _initialAnchorPositions = new Dictionary<Transform, Vector3>();
	private bool _firstPassUsesInitialAnchors = true;

	// パス回数カウント用
	private int _totalPassCount = 0; // 総パス回数
	private int _currentSessionPassCount = 0; // 現在のセッションのパス回数

	private void Awake()
	{
		// パス回数を確実に0に初期化
		_totalPassCount = 0;
		_currentSessionPassCount = 0;
	}

	private void Start()
	{
		// パス回数を0に初期化
		_totalPassCount = 0;
		_currentSessionPassCount = 0;

		// 自身にアタッチされたオブジェクトをボールとして扱う（Ball/ Ball2 など複数対応）
		var ballGo = gameObject;
		_ball = ballGo.transform;
		_rb = ballGo.GetComponent<Rigidbody>();
		if (_rb != null)
		{
			// 物理で位置が書き戻されるのを防ぐ
			_rb.isKinematic = true;
			_rb.useGravity = false;
		}

		RefreshTeammates();
		// ログ: 再生ボタン押下時点の各カプセル位置（Start直後）
		if (enableDebugLogs)
		{
			for (int i = 0; i < _teammates.Count; i++)
			{
				var t = _teammates[i];
				if (t != null)
				{
					Vector3 p = t.position;
					Debug.Log($"BallPassController: Start() 直後位置 [{i}] {t.name}: {p}");
				}
			}
		}
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: teammates={_teammates.Count}, team={passTeam}");
			Debug.Log($"BallPassController: パス回数初期化完了 (総:{_totalPassCount}, セッション:{_currentSessionPassCount})");
		}
		_waitingForStart = !autoStart;
		// 再生直後は1フレーム待ってから現在のカプセル位置を取得して初期配置
		StartCoroutine(InitializeAnchorsAtRuntime());
	}

	private System.Collections.IEnumerator InitializeAnchorsAtRuntime()
	{
		// 1フレーム（必要なら2フレーム）待って各オブジェクトの最終配置を待つ
		yield return null;
		RefreshTeammates();
		// 再生直後の各カプセル位置を保存
		_initialAnchorPositions.Clear();
		for (int i = 0; i < _teammates.Count; i++)
		{
			var t = _teammates[i];
			if (t != null && !_initialAnchorPositions.ContainsKey(t))
			{
				_initialAnchorPositions[t] = GetTargetPosition(t);
				if (enableDebugLogs)
				{
					Debug.Log($"BallPassController: 再生直後(1フレーム後) 記録位置 [{i}] {t.name}: {_initialAnchorPositions[t]} | 現在: {t.position}");
				}
			}
		}
		if (autoStart && _teammates.Count >= 2)
		{
			BeginNextPass();
		}
		else
		{
			SnapBallToTeamAnchor();
		}
	}

	private void Update()
	{
		// キーボード入力の処理
		if (enableKeyboardControl)
		{
			HandleKeyboardInput();
		}

		// 速度調整の処理
		if (enableSpeedControl)
		{
			UpdateSpeedControl();
		}

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
			// パス位置を定期的に更新（カプセルの動きに対応）
			UpdatePassPositions();

			// 進捗ベースで移動（放物線でも実速度が極力一定になるよう補正）
			if (_travelDistance < 0.0001f)
			{
				_passT = Mathf.Min(1f, _passT + Time.deltaTime);
			}
			else
			{
				// 基本の進捗増分
				float baseDeltaT = (passSpeed / _travelDistance) * Time.deltaTime;
				// 放物線の垂直成分による見かけ速度増を補正
				// dy/dt = pi*arcHeight*cos(pi*t) → 実速度係数 ≈ sqrt(1 + (dy/ds)^2)
				float k = (Mathf.PI * Mathf.Max(0f, arcHeight)) / Mathf.Max(0.001f, _travelDistance);
				float cosTerm = Mathf.Cos(_passT * Mathf.PI);
				float speedFactor = Mathf.Sqrt(1f + (k * k * cosTerm * cosTerm));
				float deltaT = baseDeltaT / speedFactor;
				_passT = Mathf.Min(1f, _passT + deltaT);
			}
			Vector3 pos = Vector3.Lerp(_startPos, _endPos, _passT);

			// 放物線の高さを計算（固定値を使用）
			// 距離に応じて放物線高さを抑制（近距離: ほぼフラット / 中距離: 緩やか）
			float dist = Mathf.Max(0f, _travelDistance);
			float maxArc = arcHeight;
			// 10mで上限そのまま、5mで50%、2mで20%程度になるようにスケール
			float scale = Mathf.Clamp01(dist / 10f);
			// 近距離の最低比率を0.35に上げ、少しだけ弧を強める
			float scaledArc = maxArc * (0.35f + 0.65f * scale);
			float heightOffset = Mathf.Sin(_passT * Mathf.PI) * scaledArc;
			pos.y += heightOffset;

			// 実移動をハード上限で制限して初期の急加速を防ぐ
			Vector3 prev = _ball.position;
			Vector3 step = pos - prev;
			float maxStep = Mathf.Max(0f, targetSpeed) * Time.deltaTime;
			if (step.magnitude > maxStep && maxStep > 0f)
			{
				pos = prev + step.normalized * maxStep;
			}
			MoveBall(pos);

			// 目標に到達したらホールドへ
			if (_passT >= 1f)
			{
				_isMoving = false;
				_holdTimer = holdTimeAtReceiver;

				// パス時の静止機能（受け手の静止を解除し、保持時間分だけ静止）
				if (enablePassPause && _teammates.Count > _currentIndex)
				{
					var toAgent = _teammates[_currentIndex].GetComponent<BasketballAgentController>();
					if (toAgent != null)
					{
						// 受け手の静止を解除してから、保持時間分だけ静止
						toAgent.SetIdleState(false, 0f);
						toAgent.SetIdleState(true, holdTimeAtReceiver);
						if (enableDebugLogs)
						{
							Debug.Log($"BallPassController: {_teammates[_currentIndex].name} の静止を解除し、{holdTimeAtReceiver}秒間保持");
						}
					}
				}

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

		// 実速度の更新
		if (_ball != null)
		{
			_actualSpeed = Vector3.Distance(_ball.position, _lastLoggedPos) / Mathf.Max(0.0001f, Time.deltaTime);
			_lastLoggedPos = _ball.position;
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

		// 初回の特別なアンカー上書きは行わない（常に現在位置ベース）

		if (Vector3.Distance(fromPos, toPos) < minPassDistance)
		{
			_currentIndex = nextIndex;
			BeginNextPass();
			return;
		}

		// ボールの現在位置から滑らかにパスを開始（ワープを防ぐ）
		// スナップ処理を削除して、ボールの現在位置を尊重

		// パス時の静止機能
		if (enablePassPause)
		{
			// 送り手を静止させる
			var fromAgent = from.GetComponent<BasketballAgentController>();
			if (fromAgent != null)
			{
				fromAgent.SetIdleState(true, passPauseDuration);
				if (enableDebugLogs)
				{
					Debug.Log($"BallPassController: {from.name} を {passPauseDuration}秒間静止");
				}
			}

			// 受け手もパス移動中は静止させる
			var toAgent = to.GetComponent<BasketballAgentController>();
			if (toAgent != null)
			{
				toAgent.SetIdleState(true, passPauseDuration);
				if (enableDebugLogs)
				{
					Debug.Log($"BallPassController: {to.name} をパス移動中に静止");
				}
			}
		}

		// エンターキー押下直後のワープを防ぐため、ボールの現在位置から開始
		_startPos = _ball.position;
		// 宛先は常に現在の受け手の位置
		_endPos = toPos;
		_travelDistance = Vector3.Distance(_startPos, _endPos);

		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: パス開始 - 開始位置: {_startPos}, 終了位置: {_endPos}, 距離: {_travelDistance}");
			Debug.Log($"BallPassController: 送り手位置: {fromPos}, ボール位置: {_ball.position}");
		}
		_passT = 0f;
		_isMoving = true;
		_currentIndex = nextIndex;

		// 以降も常に現在位置を使用

		// パス回数をカウント
		if (enablePassCounter)
		{
			_totalPassCount++;
			_currentSessionPassCount++;
			if (enableDebugLogs)
			{
				Debug.Log($"BallPassController: Pass #{_totalPassCount} started from {from.name} to {to.name}");
			}
		}

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

		// 既にパスが進行中の場合は新しいパスを開始しない
		if (_isMoving)
		{
			if (enableDebugLogs)
			{
				Debug.Log("BallPassController: 既にパスが進行中のため、新しいパスを開始しません");
			}
			return;
		}

		_holdTimer = 0f;
		BeginNextPass();
	}

	// 一時停止用API（パス回数をカウントしない）
	public void ResumePassing()
	{
		RefreshTeammates();
		// 開始フラグは直前でのみ降ろす（早期にfalseにしない）

		// エンター押下直後は1.0秒間、全エージェントの高さ変化を停止
		if (CourtManager.Instance != null)
		{
			CourtManager.Instance.FreezeHeightVariationFor(1.0f);
		}

		// エンター押下後は1.0秒間、同チームの全カプセルを完全凍結
		FreezeTeammatePositions(1.0f);
		FreezeTeammateFor(1.0f);

		// 既にパスが進行中の場合は何もしない
		if (_isMoving)
		{
			if (enableDebugLogs)
			{
				Debug.Log("BallPassController: 既にパスが進行中のため、再開処理をスキップします");
			}
			return;
		}

		// 待機中の場合のみ次のパスを開始
		if (_holdTimer > 0f)
		{
			_holdTimer = 0f;
			_waitingForStart = false;
			BeginNextPass();
		}
		else if (_waitingForStart)
		{
			// 初回のエンターキー押下時：現在の位置から次のカプセルへのパスを開始
			if (enableDebugLogs)
			{
				Debug.Log($"BallPassController: 初回エンターキー押下 - 現在のインデックス: {_currentIndex}, カプセル: {(_teammates.Count > _currentIndex ? _teammates[_currentIndex].name : "なし")}");
			}
			_waitingForStart = false;
			BeginNextPass();
		}
	}

	// 同チームの全カプセルを duration 秒だけ静止させる
	private void FreezeTeammatePositions(float duration)
	{
		for (int i = 0; i < _teammates.Count; i++)
		{
			var t = _teammates[i];
			if (t == null) continue;
			var agent = t.GetComponent<BasketballAgentController>();
			if (agent != null)
			{
				agent.SetIdleState(true, duration);
			}
		}
	}

	// 同チームの全カプセルを duration 秒だけ完全凍結（外力無視）
	private void FreezeTeammateFor(float duration)
	{
		for (int i = 0; i < _teammates.Count; i++)
		{
			var t = _teammates[i];
			if (t == null) continue;
			var agent = t.GetComponent<BasketballAgentController>();
			if (agent != null)
			{
				agent.FreezeFor(duration);
			}
		}
	}

	private void SnapBallToTeamAnchor()
	{
		if (_ball == null) return;
		RefreshTeammates();
		Transform anchor = null;
		string teamPrefix = passTeam == PassTeam.White ? "capsule-w" : "capsule-b";
		string anchorName = $"{teamPrefix}-1";

		for (int i = 0; i < _teammates.Count; i++)
		{
			if (_teammates[i].name.ToLower().Contains(anchorName))
			{
				anchor = _teammates[i];
				_currentIndex = i;
				break;
			}
		}

		// 指定されたカプセルが見つからない場合は、最初のカプセルを使用
		if (anchor == null && _teammates.Count > 0)
		{
			anchor = _teammates[0];
			_currentIndex = 0;
			if (enableDebugLogs)
			{
				Debug.LogWarning($"BallPassController: {anchorName} が見つかりません。{anchor.name} を使用します。");
			}
		}

		if (anchor != null)
		{
			MoveBall(GetTargetPosition(anchor));
		}
	}

	private Vector3 GetTargetPosition(Transform agent)
	{
		Vector3 pos = agent.position;
		var agentController = agent.GetComponent<BasketballAgentController>();

		// 着地点の精度を向上させる（CharacterControllerの中心位置を考慮）
		if (enablePreciseLanding && agentController != null)
		{
			var cc = agent.GetComponent<CharacterController>();
			if (cc != null)
			{
				// CharacterControllerの中心位置を基準にする
				pos = agent.position + cc.center;
			}
		}

		// 予測位置計算（オプション）- 着地点計算後に適用
		if (enablePrediction && agentController != null)
		{
			// エージェントの移動速度を予測に使用
			Vector3 velocity = agentController.GetCurrentVelocity();
			if (velocity.magnitude > 0.1f)
			{
				// パス速度に応じた予測時間を計算（より短い時間で精度向上）
				float predictionTime = 0.1f; // 0.1秒先を予測
				pos += velocity * predictionTime;
			}
		}

		// 着地点の高さを設定（胸の高さ）
		pos.y += targetHeight;

		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: GetTargetPosition for {agent.name} = {pos} (base: {agent.position})");
		}

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

			// HumanM_Modelはパスに参加させない
			if (name.Contains("humanm_model"))
			{
				continue;
			}

			bool isWhite = name.Contains("capsule-w");
			bool isBlack = name.Contains("capsule-b");
			if (passTeam == PassTeam.White && isWhite)
			{
				_teammates.Add(agent.transform);
				if (enableDebugLogs)
				{
					Debug.Log($"BallPassController: White team member added: {agent.gameObject.name}");
				}
			}
			else if (passTeam == PassTeam.Black && isBlack)
			{
				_teammates.Add(agent.transform);
				if (enableDebugLogs)
				{
					Debug.Log($"BallPassController: Black team member added: {agent.gameObject.name}");
				}
			}
		}
		// インデックスの正規化
		if (_teammates.Count > 0)
		{
			_currentIndex = Mathf.Clamp(_currentIndex, 0, _teammates.Count - 1);
		}
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: RefreshTeammates completed. Team: {passTeam}, Count: {_teammates.Count}");
		}
	}

	// 外部からチームを切り替えるためのAPI
	public void SetPassTeam(PassTeam team)
	{
		passTeam = team;
		RefreshTeammates();
	}

	// 速度調整の更新処理
	private void UpdateSpeedControl()
	{
		// キーボード入力による速度調整
		if (enableKeyboardControl)
		{
			HandleKeyboardInput();
		}

		// プリセットが変更された場合は適用
		ApplySpeedPreset();

		// 目標速度に向かって現在の速度を調整
		if (Mathf.Abs(passSpeed - targetSpeed) > 0.1f)
		{
			float direction = targetSpeed > passSpeed ? 1f : -1f;
			float oldSpeed = passSpeed;
			passSpeed += direction * speedAcceleration * Time.deltaTime;
			passSpeed = Mathf.Clamp(passSpeed, minSpeed, maxSpeed);

			// 速度変更時にパス位置を更新
			if (_isMoving)
			{
				// 速度が大きく変わった場合は進捗を調整
				if (Mathf.Abs(passSpeed - oldSpeed) > 0.5f)
				{
					// 現在のボール位置から実際の進捗を再計算
					if (_travelDistance > 0.0001f)
					{
						float actualProgress = Vector3.Distance(_ball.position, _startPos) / _travelDistance;
						_passT = Mathf.Clamp(actualProgress, 0f, 1f);
					}
				}
				UpdatePassPositions();
			}
		}
	}

	// キーボード入力の処理
	private void HandleKeyboardInput()
	{
		// 数字キーでプリセット選択
		if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeedPreset(SpeedPreset.VerySlow);
		if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeedPreset(SpeedPreset.Slow);
		if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeedPreset(SpeedPreset.Normal);
		if (Input.GetKeyDown(KeyCode.Alpha4)) SetSpeedPreset(SpeedPreset.Fast);
		if (Input.GetKeyDown(KeyCode.Alpha5)) SetSpeedPreset(SpeedPreset.VeryFast);

		// +/-キーで細かい調整
		if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus))
		{
			SetTargetSpeed(targetSpeed + keyboardSpeedStep * Time.deltaTime);
		}
		if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
		{
			SetTargetSpeed(targetSpeed - keyboardSpeedStep * Time.deltaTime);
		}

		// PageUp/PageDownで大きな調整
		if (Input.GetKeyDown(KeyCode.PageUp))
		{
			SetTargetSpeed(targetSpeed + keyboardSpeedStep * 3f);
		}
		if (Input.GetKeyDown(KeyCode.PageDown))
		{
			SetTargetSpeed(targetSpeed - keyboardSpeedStep * 3f);
		}

		// Rキーでパス回数をリセット
		if (Input.GetKeyDown(KeyCode.R) && enablePassCounter)
		{
			ResetPassCount();
		}

		// エンターキーでパス開始/再開
		if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
		{
			if (enableDebugLogs)
			{
				Debug.Log("BallPassController: エンターキーが押されました。パスを開始/再開します。");
			}
			ResumePassing();
		}
	}

	// 速度プリセットの適用
	private void ApplySpeedPreset()
	{
		switch (speedPreset)
		{
			case SpeedPreset.VerySlow:
				targetSpeed = 1.5f;
				break;
			case SpeedPreset.Slow:
				targetSpeed = 2.5f;
				break;
			case SpeedPreset.Normal:
				targetSpeed = 4.0f;
				break;
			case SpeedPreset.Fast:
				targetSpeed = 6.0f;
				break;
			case SpeedPreset.VeryFast:
				targetSpeed = 8.0f;
				break;
			case SpeedPreset.Custom:
				// カスタムの場合はtargetSpeedをそのまま使用
				break;
		}
	}

	// 外部から速度を設定するAPI
	public void SetTargetSpeed(float speed)
	{
		targetSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);
		speedPreset = SpeedPreset.Custom;
	}

	// 外部から速度プリセットを設定するAPI
	public void SetSpeedPreset(SpeedPreset preset)
	{
		speedPreset = preset;
		ApplySpeedPreset();
	}

	// 現在の速度を取得するAPI
	public float GetCurrentSpeed()
	{
		return passSpeed;
	}

	// 目標速度を取得するAPI
	public float GetTargetSpeed()
	{
		return targetSpeed;
	}

	// パス回数関連のAPI
	public int GetTotalPassCount()
	{
		return _totalPassCount;
	}

	public int GetCurrentSessionPassCount()
	{
		return _currentSessionPassCount;
	}

	public void ResetPassCount()
	{
		_totalPassCount = 0;
		_currentSessionPassCount = 0;
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: パス回数をリセットしました (総:{_totalPassCount}, セッション:{_currentSessionPassCount})");
		}
	}

	public void ResetCurrentSessionPassCount()
	{
		_currentSessionPassCount = 0;
		if (enableDebugLogs)
		{
			Debug.Log("BallPassController: セッション内パス回数をリセットしました");
		}
	}

	public void SetPassCounterEnabled(bool enabled)
	{
		enablePassCounter = enabled;
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: パスカウンターを{(enabled ? "有効" : "無効")}にしました");
		}
	}

	// 開始カプセル番号関連APIは廃止

	// 予測機能のオンオフを設定するAPI
	public void SetPredictionEnabled(bool enabled)
	{
		enablePrediction = enabled;
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: 予測機能を{(enabled ? "有効" : "無効")}にしました");
		}
	}

	// 予測機能の状態を取得するAPI
	public bool IsPredictionEnabled()
	{
		return enablePrediction;
	}

	// 着地点精度設定のAPI
	public void SetPreciseLandingEnabled(bool enabled)
	{
		enablePreciseLanding = enabled;
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: 着地点精度を{(enabled ? "有効" : "無効")}にしました");
		}
	}

	// 着地点精度の状態を取得するAPI
	public bool IsPreciseLandingEnabled()
	{
		return enablePreciseLanding;
	}

	// パス時の静止機能の設定API
	public void SetPassPauseEnabled(bool enabled)
	{
		enablePassPause = enabled;
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: パス時の静止機能を{(enabled ? "有効" : "無効")}にしました");
		}
	}

	// パス時の静止時間を設定するAPI
	public void SetPassPauseDuration(float duration)
	{
		passPauseDuration = Mathf.Clamp(duration, 0.1f, 3.0f);
		if (enableDebugLogs)
		{
			Debug.Log($"BallPassController: パス時の静止時間を {passPauseDuration}秒 に設定しました");
		}
	}

	// パス時の静止機能の状態を取得するAPI
	public bool IsPassPauseEnabled()
	{
		return enablePassPause;
	}

	// パス時の静止時間を取得するAPI
	public float GetPassPauseDuration()
	{
		return passPauseDuration;
	}

	// ボールが動いているかどうかを取得
	public bool IsMoving()
	{
		return _isMoving;
	}

	// 現在の目標位置を取得
	public Vector3 GetCurrentTargetPosition()
	{
		return _endPos;
	}


	// パス位置を動的に更新するメソッド
	private void UpdatePassPositions()
	{
		if (_teammates.Count < 2) return;

		// 現在の送り手と受け手の位置を再計算
		Transform from = _teammates[_currentIndex];
		Transform to = _teammates[(_currentIndex + 1) % _teammates.Count];

		Vector3 newFromPos = GetTargetPosition(from);
		Vector3 newToPos = GetTargetPosition(to);

		// 終了位置を常に更新（受け手の動きに対応）
		_endPos = newToPos;

		// 開始位置はパス開始直後のみ、現在のボール位置で補正（ワープ防止）
		if (_passT < 0.1f)
		{
			_startPos = _ball.position;
		}

		_travelDistance = Vector3.Distance(_startPos, _endPos);

		if (enableDebugLogs && _travelDistance > 0.1f)
		{
			Debug.Log($"BallPassController: パス位置を更新 - 進捗:{_passT:F2}, 開始:{_startPos}, 終了:{_endPos}, 距離:{_travelDistance:F2}");
		}
	}

	// 速度情報とパス回数を画面に表示
	private void OnGUI()
	{
		// 速度情報の表示（実測速度を追加）
		if (showSpeedInfo)
		{
			GUILayout.BeginArea(new Rect(10, 10, 300, 200));
			GUILayout.Label("=== ボール速度調整 ===", GUI.skin.box);
			GUILayout.Label($"目標速度(設定): {targetSpeed:F1} m/s");
			GUILayout.Label($"実測速度: {_actualSpeed:F2} m/s");
			GUILayout.Label($"目標速度: {targetSpeed:F1} m/s");
			GUILayout.Label($"プリセット: {speedPreset}");
			GUILayout.Space(10);
			GUILayout.Label("キーボード操作:");
			GUILayout.Label("1-5: プリセット選択");
			GUILayout.Label("+/-: 細かい調整");
			GUILayout.Label("PageUp/Down: 大きな調整");
			GUILayout.EndArea();
		}

		// パス回数の表示
		if (enablePassCounter && showPassCount)
		{
			// フォントサイズを設定
			GUIStyle style = new GUIStyle(GUI.skin.label);
			style.fontSize = passCountFontSize;
			style.normal.textColor = Color.white;

			GUILayout.BeginArea(new Rect(passCountDisplayOffset.x, passCountDisplayOffset.y, 300, 100));
			GUILayout.Label("=== パス回数 ===", GUI.skin.box);
			GUILayout.Label($"総パス回数: {_totalPassCount}", style);
			GUILayout.Label($"セッション内: {_currentSessionPassCount}", style);
			GUILayout.Space(5);
			GUILayout.Label("Rキー: リセット");
			GUILayout.EndArea();
		}
	}
}


