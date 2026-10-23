using UnityEngine;

public class WhiteboxDoor : MonoBehaviour
{
    public float targetAngle;
    public float openAngle = 88;
    public bool IsOpen => Mathf.Abs(Mathf.DeltaAngle(transform.localEulerAngles.y, openAngle)) < 2;
    public void Toggle() { targetAngle = targetAngle > 40 ? 0 : openAngle; }
    public void SetImmediate(bool open)
    {
        targetAngle = open ? openAngle : 0;
        transform.localRotation = Quaternion.Euler(0, targetAngle, 0);
        Physics.SyncTransforms();
    }
    void Update()
    {
        float angle = Mathf.MoveTowardsAngle(transform.localEulerAngles.y, targetAngle, 90 * Time.deltaTime);
        Quaternion next = Quaternion.Euler(0, angle, 0);
        Vector3 center = transform.position + next * new Vector3(.47f, 1.08f, 0);
        foreach (var c in Physics.OverlapBox(center, new Vector3(.47f, 1.08f, .07f), next))
            if (c is CharacterController) return;
        transform.localRotation = next;
    }
}
