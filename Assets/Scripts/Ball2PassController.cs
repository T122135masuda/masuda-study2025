using System.Collections.Generic;
using UnityEngine;

public class Ball2PassController : MonoBehaviour
{
  public enum PassTeam
  {
    White,
    Black
  }

  [Header("Pass Settings")]
  [Tooltip("どのチーム同士でパスするか（White/Black）")]
  public PassTeam passTeam = PassTeam.Black;
  [Tooltip("パス開始時の基準カプセル番号（1, 2, 3など）")]
  public int startCapsuleNumber = 2;
  [Tooltip("待機（エンター押下前）に表示するカプセル番号。未指定時はstartCapsuleNumberを使用")]
  public int idleCapsuleNumber = 0;
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
  public float passSpeed = 4.0f;
  [Tooltip("受け手に到達後に保持する時間（秒）")]
  public float holdTimeAtReceiver = 0.8f;
  [Tooltip("パス時の放物線の高さ（m）")]
  public float arcHeight = 0.7f;
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
  public float speedAcceleration = 2.0f;
  [Tooltip("速度変化の減速度（m/s²）")]
  public float speedDeceleration = 3.0f;
  [Tooltip("目標速度に到達するまでの時間（秒）")]
  public float speedSmoothingTime = 0.3f;

  [Header("Debug")]
  [Tooltip("デバッグログを表示する")]
  public bool enableDebugLogs = true;
  [Tooltip("キーボード制御を有効にする")]
  public bool enableKeyboardControl = true;
  [Tooltip("移動ログを表示する")]
  public bool logMovement = false;
  [Tooltip("移動ログの間隔（秒）")]
  public float movementLogInterval = 0.1f;
  [Tooltip("速度情報を表示する")]
  public bool showSpeedInfo = true;

  private float _movementLogTimer = 0f;
  private Vector3 _lastLoggedPos;

  private Transform _ball; // シーンの Ball2 オブジェクト
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

    // Ball2 を名前で取得（大文字小文字や部分一致にもある程度対応）
    var ballGo = GameObject.Find("Ball2");
    if (ballGo == null)
    {
      foreach (var t in FindObjectsOfType<Transform>())
      {
        if (t.name.Equals("ball2", System.StringComparison.OrdinalIgnoreCase))
        {
          ballGo = t.gameObject;
          break;
        }
      }
    }
    if (ballGo == null)
    {
      Debug.LogWarning("Ball2PassController: 'Ball2' オブジェクトが見つかりません。");
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
      Debug.Log($"Ball2PassController: teammates={_teammates.Count}, team={passTeam}");
      Debug.Log($"Ball2PassController: CourtManager.Instance={CourtManager.Instance}");
      Debug.Log($"Ball2PassController: パス回数初期化完了 (総:{_totalPassCount}, セッション:{_currentSessionPassCount})");

      // 各teammateの詳細をログ出力
      for (int i = 0; i < _teammates.Count; i++)
      {
        if (_teammates[i] != null)
        {
          Debug.Log($"Ball2PassController: teammate[{i}] = {_teammates[i].name}");
        }
      }
    }

    _waitingForStart = !autoStart;
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: autoStart={autoStart}, teammates.Count={_teammates.Count}, waitingForStart={_waitingForStart}");
    }

    if (autoStart && _teammates.Count >= 2)
    {
      if (enableDebugLogs)
      {
        Debug.Log("Ball2PassController: autoStart=true かつ teammates>=2 なので StartPassing() を呼び出します");
      }
      StartPassing();
    }
    else
    {
      if (enableDebugLogs)
      {
        Debug.Log("Ball2PassController: 待機状態。SnapBallToTeamAnchor() を呼び出します");
      }
      // 待機中はチームの基準アンカー上にボールを固定
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
        Debug.Log($"Ball2PassController: refreshed teammates={_teammates.Count}");
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
      UpdatePassMovement();
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
            Debug.Log($"Ball2PassController: moving={_isMoving}, pos={_ball.position}");
          }
        }
      }
    }
  }

  private void UpdatePassMovement()
  {
    // 進捗ベースで移動（速度 → 時間あたりの進捗量に変換）
    float deltaT = _travelDistance > 0.0001f ? (passSpeed / _travelDistance) * Time.deltaTime : 1f * Time.deltaTime;
    _passT = Mathf.Min(1f, _passT + deltaT);
    Vector3 pos = Vector3.Lerp(_startPos, _endPos, _passT);

    // 放物線の高さを計算（固定値を使用）
    float heightOffset = Mathf.Sin(_passT * Mathf.PI) * arcHeight;
    pos.y += heightOffset;
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
            Debug.Log($"Ball2PassController: {_teammates[_currentIndex].name} の静止を解除し、{holdTimeAtReceiver}秒間保持");
          }
        }
      }

      if (enableDebugLogs)
      {
        Debug.Log($"Ball2PassController: arrived receiver index={_currentIndex}, pos={_ball.position}");
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
            Debug.Log($"Ball2PassController: moving={_isMoving}, pos={_ball.position}");
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
        Debug.LogWarning("Ball2PassController: teammates < 2 でパスできません。");
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
          Debug.Log($"Ball2PassController: {from.name} を {passPauseDuration}秒間静止");
        }
      }

      // 受け手もパス移動中は静止させる
      var toAgent = to.GetComponent<BasketballAgentController>();
      if (toAgent != null)
      {
        toAgent.SetIdleState(true, passPauseDuration);
        if (enableDebugLogs)
        {
          Debug.Log($"Ball2PassController: {to.name} をパス移動中に静止");
        }
      }
    }

    // パス開始
    // エンターキー押下直後のワープを防ぐため、ボールの現在位置から開始
    _startPos = _ball.position;
    _endPos = toPos;
    _travelDistance = Vector3.Distance(_startPos, _endPos);

    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: パス開始 - 開始位置: {_startPos}, 終了位置: {_endPos}, 距離: {_travelDistance}");
      Debug.Log($"Ball2PassController: 送り手位置: {fromPos}, ボール位置: {_ball.position}");
    }
    _passT = 0f;
    _isMoving = true;
    _currentIndex = nextIndex;

    // パス回数カウント
    _totalPassCount++;
    _currentSessionPassCount++;

    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: Pass #{_totalPassCount} started from {from.name} to {to.name}");
    }

    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: pass {_startPos} -> {_endPos}, dist={_travelDistance}");
    }
  }

  private void MoveBall(Vector3 position)
  {
    if (_ball == null) return;

    // 速度調整が有効な場合
    if (enableSpeedControl)
    {
      UpdateSpeedControl();
    }

    _ball.position = position;
  }


  private void StartPassing()
  {
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: StartPassing() が呼び出されました。teammates.Count={_teammates.Count}");
    }

    if (_teammates.Count < 2)
    {
      if (enableDebugLogs)
      {
        Debug.LogWarning("Ball2PassController: teammates < 2 でパスを開始できません。");
      }
      return;
    }

    _waitingForStart = false;

    // 既にパスが進行中の場合は何もしない
    if (_isMoving)
    {
      if (enableDebugLogs)
      {
        Debug.Log("Ball2PassController: 既にパスが進行中のため、新しいパスを開始しません");
      }
      return;
    }

    if (enableDebugLogs)
    {
      Debug.Log("Ball2PassController: BeginNextPass() を呼び出します");
    }
    BeginNextPass();
  }

  private void ResumePassing()
  {
    if (_teammates.Count < 2)
    {
      if (enableDebugLogs)
      {
        Debug.LogWarning("Ball2PassController: teammates < 2 でパスを再開できません。");
      }
      return;
    }

    _waitingForStart = false;

    // 既にパスが進行中の場合は何もしない
    if (_isMoving)
    {
      if (enableDebugLogs)
      {
        Debug.Log("Ball2PassController: 既にパスが進行中のため、再開処理をスキップします");
      }
      return;
    }

    // 待機中の場合のみ次のパスを開始
    if (_holdTimer > 0f)
    {
      _holdTimer = 0f;
      BeginNextPass();
    }
    else if (_waitingForStart)
    {
      // 初回のエンターキー押下時：現在の位置から次のカプセルへのパスを開始
      if (enableDebugLogs)
      {
        Debug.Log($"Ball2PassController: 初回エンターキー押下 - 現在のインデックス: {_currentIndex}, カプセル: {(_teammates.Count > _currentIndex ? _teammates[_currentIndex].name : "なし")}");
      }
      BeginNextPass();
    }
  }

  private void SnapBallToTeamAnchor()
  {
    if (_ball == null) return;
    RefreshTeammates();
    Transform anchor = null;
    string teamPrefix = passTeam == PassTeam.White ? "capsule-w" : "capsule-b";
    int anchorNumber = idleCapsuleNumber > 0 ? idleCapsuleNumber : startCapsuleNumber;
    string anchorName = $"{teamPrefix}-{anchorNumber}";

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
        Debug.LogWarning($"Ball2PassController: {anchorName} が見つかりません。{anchor.name} を使用します。");
      }
    }

    if (anchor != null)
    {
      MoveBall(GetTargetPosition(anchor));
      if (enableDebugLogs)
      {
        Debug.Log($"Ball2PassController: パス開始位置を {anchor.name} に設定しました (idleCapsuleNumber={(idleCapsuleNumber > 0 ? idleCapsuleNumber : startCapsuleNumber)})");
      }
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
      Debug.Log($"Ball2PassController: GetTargetPosition for {agent.name} = {pos} (base: {agent.position})");
    }

    return pos;
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

    // 開始位置はパスの進捗に応じて更新
    if (_passT < 0.3f) // パスの初期段階でのみ開始位置を更新
    {
      _startPos = newFromPos;
      _travelDistance = Vector3.Distance(_startPos, _endPos);
    }
    else
    {
      // パスが進行中の場合は距離を再計算
      _travelDistance = Vector3.Distance(_startPos, _endPos);
    }
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
          Debug.Log($"Ball2PassController: White team member added: {agent.gameObject.name}");
        }
      }
      else if (passTeam == PassTeam.Black && isBlack)
      {
        _teammates.Add(agent.transform);
        if (enableDebugLogs)
        {
          Debug.Log($"Ball2PassController: Black team member added: {agent.gameObject.name}");
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
      Debug.Log($"Ball2PassController: RefreshTeammates completed. Team: {passTeam}, Count: {_teammates.Count}");
    }
  }

  // 外部からチームを切り替えるためのAPI
  public void SetPassTeam(PassTeam team)
  {
    passTeam = team;
    RefreshTeammates();
  }

  // キーボード入力の処理
  private void HandleKeyboardInput()
  {
    // エンターキーでパス開始/再開
    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
    {
      if (enableDebugLogs)
      {
        Debug.Log("Ball2PassController: エンターキーが押されました。パスを開始/再開します。");
      }
      ResumePassingExternal();
    }
  }

  // 速度調整の更新処理
  private void UpdateSpeedControl()
  {
    if (!enableSpeedControl || _ball == null) return;

    // 現在の移動速度を計算
    float currentSpeed = Vector3.Distance(_ball.position, _lastLoggedPos) / Time.deltaTime;

    // 目標速度を設定
    float targetSpeed = Mathf.Clamp(passSpeed, minSpeed, maxSpeed);

    // 速度調整
    if (currentSpeed < targetSpeed)
    {
      passSpeed = Mathf.MoveTowards(passSpeed, targetSpeed, speedAcceleration * Time.deltaTime);
    }
    else if (currentSpeed > targetSpeed)
    {
      passSpeed = Mathf.MoveTowards(passSpeed, targetSpeed, speedDeceleration * Time.deltaTime);
    }

    _lastLoggedPos = _ball.position;

    if (showSpeedInfo && enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: Current Speed: {currentSpeed:F2} m/s, Target: {targetSpeed:F2} m/s, Pass Speed: {passSpeed:F2} m/s");
    }
  }

  // 外部API
  public void StartPassingExternal()
  {
    StartPassing();
  }

  public void StopPassing()
  {
    _isMoving = false;
    _waitingForStart = true;
    _holdTimer = 0f;
    _passT = 0f;
  }

  public void PausePassing()
  {
    _waitingForStart = true;
  }

  public void ResumePassingExternal()
  {
    ResumePassing();
  }

  public bool IsMoving()
  {
    return _isMoving;
  }

  public Vector3 GetCurrentPosition()
  {
    return _ball != null ? _ball.position : Vector3.zero;
  }

  public Vector3 GetCurrentTargetPosition()
  {
    if (_teammates.Count > _currentIndex)
    {
      return GetTargetPosition(_teammates[_currentIndex]);
    }
    return Vector3.zero;
  }

  public float GetCurrentSpeed()
  {
    return passSpeed;
  }

  // パス回数カウントAPI
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
      Debug.Log($"Ball2PassController: パス回数をリセットしました (総:{_totalPassCount}, セッション:{_currentSessionPassCount})");
    }
  }

  public void ResetSessionPassCount()
  {
    _currentSessionPassCount = 0;
    if (enableDebugLogs)
    {
      Debug.Log("Ball2PassController: セッション内パス回数をリセットしました");
    }
  }

  public void SetPassCounterEnabled(bool enabled)
  {
    // パスカウンターの有効/無効を設定
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: パスカウンターを{(enabled ? "有効" : "無効")}にしました");
    }
  }

  public void SetStartCapsuleNumber(int startCapsuleNumber)
  {
    this.startCapsuleNumber = startCapsuleNumber;
    RefreshTeammates();
    SnapBallToTeamAnchor();
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: 開始カプセル番号を {startCapsuleNumber} に設定しました");
    }
  }

  public void SetPredictionEnabled(bool enabled)
  {
    enablePrediction = enabled;
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: 予測機能を{(enabled ? "有効" : "無効")}にしました");
    }
  }

  public void SetPreciseLandingEnabled(bool enabled)
  {
    enablePreciseLanding = enabled;
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: 着地点精度を{(enabled ? "有効" : "無効")}にしました");
    }
  }

  public void SetPassPauseEnabled(bool enabled)
  {
    enablePassPause = enabled;
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: パス時の静止機能を{(enabled ? "有効" : "無効")}にしました");
    }
  }

  public void SetPassPauseDuration(float duration)
  {
    passPauseDuration = Mathf.Clamp(duration, 0.1f, 3.0f);
    if (enableDebugLogs)
    {
      Debug.Log($"Ball2PassController: パス時の静止時間を {passPauseDuration}秒 に設定しました");
    }
  }

  // パス位置の取得（デバッグ用）
  public Vector3 GetPassStartPosition()
  {
    return _startPos;
  }

  public Vector3 GetPassEndPosition()
  {
    return _endPos;
  }

  public float GetPassProgress()
  {
    return _passT;
  }

  public float GetPassTravelDistance()
  {
    return _travelDistance;
  }

  // パス位置の更新（デバッグ用）
  public void UpdatePassPosition()
  {
    if (_isMoving && _passT < 1f)
    {
      float actualProgress = Vector3.Distance(_ball.position, _startPos) / _travelDistance;
      _passT = Mathf.Clamp01(actualProgress);
    }
  }
}
