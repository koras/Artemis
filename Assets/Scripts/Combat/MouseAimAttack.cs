using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class MouseAimAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform aimPivot;

    [Header("Animator")]
    [SerializeField] private string attackBool = "AttackActive";
    [SerializeField] private string speedFloat = "Speed";
    [SerializeField] private string shootState = "Shoot";
    [SerializeField] private string idleState = "idle";
    [SerializeField] private string moveState = "Move";

    [Header("Aiming")]
    [SerializeField] private float angleOffset;
    [SerializeField] private float shootPlaybackSpeed = 1f;
    [SerializeField] private bool aimOnlyDuringShoot = true;
    [SerializeField] private bool flipRootToMouse;

    private int attackBoolHash;
    private bool attackEnabled;
    private float attackStartTime;
    private Vector2 currentAimDirection = Vector2.right;
    private Vector3 authoredLocalPosition;

    private void Awake()
    {
        // Preserve the prefab-authored local offset because the imported clips animate
        // the root transform and otherwise snap this object back to the clip origin.
        authoredLocalPosition = transform.localPosition;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        attackBoolHash = Animator.StringToHash(attackBool);
        animator.SetBool(attackBoolHash, attackEnabled);
    }

    private void Update()
    {
        if (TryGetMouseWorldPosition(out Vector3 mouseWorld))
        {
            Vector2 direction = mouseWorld - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                currentAimDirection = direction.normalized;
            }
        }

        if (WasTogglePressed())
        {
            attackEnabled = !attackEnabled;
            animator.SetBool(attackBoolHash, attackEnabled);

            if (attackEnabled)
            {
                attackStartTime = Time.time;
            }
            else
            {
                animator.CrossFade(GetLocomotionStateName(), 0.05f, 0);
            }
        }
    }

    private void LateUpdate()
    {
        if (attackEnabled)
        {
            float normalizedTime = Mathf.Repeat((Time.time - attackStartTime) * shootPlaybackSpeed, 1f);
            animator.Play(shootState, 0, normalizedTime);
        }

        transform.localPosition = authoredLocalPosition;

        if (attackEnabled || !aimOnlyDuringShoot || IsInShootState())
        {
            ApplyAim(currentAimDirection);
        }
    }

    private void ApplyAim(Vector2 direction)
    {
        if (aimPivot == null || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        bool facingLeft = false;

        if (flipRootToMouse)
        {
            Vector3 scale = transform.localScale;
            facingLeft = direction.x < 0f;
            scale.x = Mathf.Abs(scale.x) * (facingLeft ? -1f : 1f);
            transform.localScale = scale;
        }

        Vector2 localAimDirection = facingLeft
            ? new Vector2(-direction.x, direction.y)
            : direction;

        float angle = Mathf.Atan2(localAimDirection.y, localAimDirection.x) * Mathf.Rad2Deg + angleOffset;
        aimPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;
        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Vector2 screenPosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
        }
        else
#endif
        {
            screenPosition = Input.mousePosition;
        }

        float distanceFromCamera = Mathf.Abs(cameraToUse.transform.position.z - transform.position.z);
        Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera);
        worldPosition = cameraToUse.ScreenToWorldPoint(screenPoint);
        worldPosition.z = transform.position.z;
        return true;
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    private bool IsInShootState()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(shootState))
        {
            return true;
        }

        if (!animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return next.IsName(shootState);
    }

    private string GetLocomotionStateName()
    {
        if (animator == null)
        {
            return idleState;
        }

        return animator.GetFloat(speedFloat) > 0.05f ? moveState : idleState;
    }
}
