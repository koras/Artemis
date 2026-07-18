using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnimatorStateDebugOverlay : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer[] watchedRenderers;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private Vector2 screenPosition = new Vector2(12f, 12f);
    [SerializeField] private Vector2 size = new Vector2(420f, 180f);

    private readonly StringBuilder builder = new StringBuilder(512);
    private GUIStyle labelStyle;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void OnGUI()
    {
        if (!showOverlay || animator == null)
        {
            return;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };
        }

        Rect rect = new Rect(screenPosition.x, screenPosition.y, size.x, size.y);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), BuildText(), labelStyle);
    }

    private string BuildText()
    {
        builder.Length = 0;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        builder.Append("Animator State: ").Append(GetKnownStateName(current)).Append('\n');
        builder.Append("Normalized Time: ").Append(current.normalizedTime.ToString("F2")).Append('\n');
        builder.Append("In Transition: ").Append(animator.IsInTransition(0)).Append('\n');

        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];
            builder.Append(parameter.name).Append(": ");
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    builder.Append(animator.GetFloat(parameter.name).ToString("F2"));
                    break;
                case AnimatorControllerParameterType.Int:
                    builder.Append(animator.GetInteger(parameter.name));
                    break;
                case AnimatorControllerParameterType.Bool:
                    builder.Append(animator.GetBool(parameter.name));
                    break;
                case AnimatorControllerParameterType.Trigger:
                    builder.Append("Trigger");
                    break;
            }
            builder.Append('\n');
        }

        if (watchedRenderers != null && watchedRenderers.Length > 0)
        {
            builder.Append("Renderers:\n");
            for (int i = 0; i < watchedRenderers.Length; i++)
            {
                if (watchedRenderers[i] == null)
                {
                    continue;
                }

                builder.Append("  ")
                    .Append(watchedRenderers[i].name)
                    .Append(": ")
                    .Append(watchedRenderers[i].enabled)
                    .Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string GetKnownStateName(AnimatorStateInfo state)
    {
        if (state.IsName("idle")) return "idle";
        if (state.IsName("Move")) return "Move";
        if (state.IsName("Work")) return "Work";
        if (state.IsName("Eating")) return "Eating";
        return "Unknown (hash " + state.shortNameHash + ")";
    }
}
