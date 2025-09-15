using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("VR Camera Settings")]
    public float fieldOfView = 90f; // VR標準視野角
    public float nearClipPlane = 0.01f; // VR用近接面
    public float farClipPlane = 1000f;
    
    [Header("Mouse Sensitivity")]
    public float mouseSensitivity = 2.0f;
    public float maxLookUpAngle = 80f;
    public float maxLookDownAngle = -80f;
    
    [Header("Head Offset")]
    public Vector3 headOffset = new Vector3(-4.35f, 1.37f, 0.992f); // 頭部からのオフセット
    public bool autoAdjustOffset = true; // オフセットを自動調整
    
    [Header("VR Specific")]
    public bool enableVRMode = true; // VRモード有効
    public float vrIPD = 0.064f; // 瞳間距離（64mm標準）
    public bool enableStereoRendering = true; // ステレオレンダリング
    
    private Camera _camera;
    private float _rotationX = 0f;
    private float _rotationY = 0f;
    private Transform _headTransform;
    
    private void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            _camera = gameObject.AddComponent<Camera>();
        }
        
        // 頭部のTransformを取得（このスクリプトがアタッチされているオブジェクト）
        _headTransform = transform;
        
        // VR用カメラ設定
        SetupVRCamera();
        
        // カーソルをロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // 初期位置を設定
        SetupInitialPosition();
        
        Debug.Log("FirstPersonCamera: head-cameraオブジェクトにアタッチされました");
    }
    
    private void SetupInitialPosition()
    {
        // head-cameraオブジェクトの実際の位置を取得
        Vector3 cameraWorldPosition = transform.position;
        Debug.Log($"FirstPersonCamera: head-cameraの実際の位置: {cameraWorldPosition}");
        
        // 自動調整が有効な場合、オフセットを調整
        if (autoAdjustOffset)
        {
            // head-cameraの親の位置を基準にオフセットを計算
            Transform parent = transform.parent;
            if (parent != null)
            {
                Vector3 parentPosition = parent.position;
                Vector3 adjustedOffset = cameraWorldPosition - parentPosition;
                headOffset = adjustedOffset;
                Debug.Log($"FirstPersonCamera: オフセットを自動調整しました - 新しいオフセット: {headOffset}");
            }
        }
        
        // オフセットを適用（ローカル座標系）
        transform.localPosition = headOffset;
        
        // 初期回転を右に90度回転させた向きに設定
        transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        
        // 初期回転値を保存（右に90度回転）
        _rotationX = 0f;
        _rotationY = 90f;
        
        Debug.Log($"FirstPersonCamera: 初期位置を設定しました - ローカル位置: {transform.localPosition}, ワールド位置: {transform.position}, 初期回転: 右90度");
    }
    
    private void SetupVRCamera()
    {
        // VR用の基本設定
        _camera.clearFlags = CameraClearFlags.Skybox;
        _camera.fieldOfView = fieldOfView;
        _camera.nearClipPlane = nearClipPlane;
        _camera.farClipPlane = farClipPlane;
        
        // VR特有の設定
        if (enableVRMode)
        {
            // VR用の投影設定
            _camera.projectionMatrix = Matrix4x4.Perspective(fieldOfView, _camera.aspect, nearClipPlane, farClipPlane);
            
            // VR用の深度設定
            _camera.depth = 0;
            _camera.renderingPath = RenderingPath.Forward;
            
            // VR用のアンチエイリアシング
            QualitySettings.antiAliasing = 4; // 4x MSAA
        }
    }
    
    private void Update()
    {
        // マウス入力を取得
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // 回転を更新
        _rotationY += mouseX;
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, maxLookDownAngle, maxLookUpAngle);
        
        // カメラの回転を更新（ローカル回転として）
        transform.localRotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
        
        // ESCキーでカーソルロック解除
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursorLock();
        }
    }
    
    private void ToggleCursorLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    // 外部からカメラの有効/無効を切り替え
    public void SetCameraActive(bool active)
    {
        if (_camera != null)
        {
            _camera.enabled = active;
        }
    }
    
    // VR設定を動的に変更
    public void SetVRMode(bool enabled)
    {
        enableVRMode = enabled;
        SetupVRCamera();
    }
    
    // 視野角を動的に変更
    public void SetFieldOfView(float newFOV)
    {
        fieldOfView = Mathf.Clamp(newFOV, 60f, 120f);
        if (_camera != null)
        {
            _camera.fieldOfView = fieldOfView;
        }
    }
    
    // オフセットを動的に変更
    public void SetHeadOffset(Vector3 newOffset)
    {
        headOffset = newOffset;
        transform.localPosition = headOffset;
    }
}
