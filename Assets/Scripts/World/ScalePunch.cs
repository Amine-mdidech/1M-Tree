using UnityEngine;

// Reusable bouncy scale-pop effect. Lives as a sibling component so it keeps animating even if
// another component on the same object (e.g. PlantedTree) disables its own Update for perf once
// idle - the two components' enabled states are independent.
public class ScalePunch : MonoBehaviour
{
    private Transform target;
    private float baseScale;
    private float duration;
    private float overshoot;
    private float age;

    public static void Punch(Transform target, float baseScale, float duration = 0.25f, float overshoot = 1.35f)
    {
        ScalePunch punch = target.GetComponent<ScalePunch>();
        if (punch == null)
        {
            punch = target.gameObject.AddComponent<ScalePunch>();
        }

        punch.target = target;
        punch.baseScale = baseScale;
        punch.duration = Mathf.Max(0.05f, duration);
        punch.overshoot = overshoot;
        punch.age = 0f;
        punch.enabled = true;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float pulse = Mathf.Sin(t * Mathf.PI) * (overshoot - 1f);
        float scale = baseScale * (1f + pulse);
        target.localScale = new Vector3(scale, scale, 1f);

        if (t >= 1f)
        {
            target.localScale = new Vector3(baseScale, baseScale, 1f);
            Destroy(this);
        }
    }
}
