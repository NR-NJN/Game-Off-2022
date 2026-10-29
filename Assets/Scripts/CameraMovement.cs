using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    public GameObject follow;
    public Vector3 offset = new Vector3(0f, 1f, -5f);
    public Vector2 maxRotation = new Vector2(5f, 5f);

    public float smoothTime = .3f;
    public bool snap = false;
    public bool trackXAxis = true;
    public bool trackYAxis = true;
    public bool trackZAxis = true;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        Vector3 followVec = this.follow.transform.position + offset;
        Vector3 destination = new Vector3(
            (trackXAxis) ? followVec.x : this.transform.position.x,
            (trackYAxis) ? followVec.y : this.transform.position.y,
            (trackZAxis) ? followVec.z : this.transform.position.z
        );

        this.transform.position = (this.snap) ? destination : Vector3.SmoothDamp(
            this.transform.position, destination, ref velocity, smoothTime
        );

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2 pointer = (Pointer.current != null) ? Pointer.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) / 2f;

        this.transform.rotation = Quaternion.Euler(
            maxRotation.x * (Mathf.Clamp01(pointer.y / Screen.height) * 2 - 1) * -1,
            maxRotation.y * (Mathf.Clamp01(pointer.x / Screen.width) * 2 - 1),
            0f
        );
    }
}
