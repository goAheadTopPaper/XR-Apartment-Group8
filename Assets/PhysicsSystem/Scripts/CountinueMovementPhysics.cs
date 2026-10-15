using UnityEngine;
using UnityEngine.InputSystem;

public class CountinueMovementPhysics : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 1f;
    public InputActionProperty moveInputSource;
    public InputActionProperty snapTurnInputSource;

    [Header("Character Controller")]
    public CharacterController cc;
    public LayerMask groundLayer;

    [Header("VR 胶囊自适应高度")]
    [Tooltip("VR头盔主相机")]
    public Transform cam;
    [Tooltip("胶囊最小高度（蹲下）")]
    public float minCapsuleHeight = 0.4f;
    [Tooltip("胶囊最大高度（站立）")]
    public float maxCapsuleHeight = 2.0f;
    [Tooltip("胶囊半径固定值")]
    public float capsuleRadius = 0.1f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    public float jumpHeight = 1f;
    private Vector3 velocity;

    [Header("Direction")]
    public Transform directionSource;

    [Header("Snap Turn")]
    [SerializeField] private float snapTurnAngle = 45f;
    [SerializeField] private float snapTurnThreshold = 0.7f;
    [SerializeField] private Transform turnSource;

    private Vector2 inputMoveAxis;
    private float inputSnapTurnAxis;
    private bool snapTurnReady = true;


    private void Update()
    {
        UpdateCapsuleHeightToCameraGroundHeight();

        inputMoveAxis = moveInputSource.action.ReadValue<Vector2>();
        inputSnapTurnAxis = snapTurnInputSource.action.ReadValue<Vector2>().x;

        HandleMovement();
        HandleSnapTurn();
    }

    /// <summary>
    /// 将CharacterController胶囊高度设置为【相机到地面的高度】
    /// </summary>
    void UpdateCapsuleHeightToCameraGroundHeight()
    {
        if (cc == null || cam == null) return;

        // 相机世界Y
        float camWorldY = cam.position.y;
        // 角色根物体脚底Y（CharacterController物体原点地面）
        float rootGroundY = transform.position.y;

        // 相机距离地面高度
        float eyeHeight = camWorldY - rootGroundY;

        // 钳制最大最小高度，防止蹲下/站起异常
        float targetHeight = Mathf.Clamp(eyeHeight, minCapsuleHeight, maxCapsuleHeight);

        cc.radius = capsuleRadius;
        cc.height = targetHeight;
        // center：胶囊中心点Y = 高度的一半，胶囊底部对齐脚底
        cc.center = new Vector3(0f, targetHeight * 0.5f, 0f);
    }


    private void HandleMovement()
    {
        bool isGrounded = cc.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Quaternion yaw = Quaternion.Euler(0f, directionSource.eulerAngles.y, 0f);
        Vector3 moveDir = yaw * new Vector3(inputMoveAxis.x, 0f, inputMoveAxis.y);

        Vector3 horizontalMotion = moveDir * moveSpeed;

        velocity.y += gravity * Time.deltaTime;

        // 只调用一次 cc.Move！分开调用会破坏isGrounded状态，导致掉地下
        cc.Move((horizontalMotion + velocity) * Time.deltaTime);
    }

    private void HandleSnapTurn()
    {
        if (Mathf.Abs(inputSnapTurnAxis) < snapTurnThreshold)
        {
            snapTurnReady = true;
            return;
        }
        if (!snapTurnReady)
            return;

        float turnAngle = inputSnapTurnAxis > 0f ? snapTurnAngle : -snapTurnAngle;
        RotateAroundTurnSource(turnAngle);
        snapTurnReady = false;
    }

    private void RotateAroundTurnSource(float turnAngle)
    {
        Quaternion turnRotation = Quaternion.AngleAxis(turnAngle, Vector3.up);
        transform.rotation = turnRotation * transform.rotation;
        Vector3 newPos = turnRotation * (transform.position - turnSource.position) + turnSource.position;
        transform.position = newPos;
    }
}
