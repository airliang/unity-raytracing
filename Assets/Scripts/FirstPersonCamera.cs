using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class FirstPersonCamera : MonoBehaviour
{
    Vector3 lastFrameMousePosition;
    public float moveSpeed = 10.0f;
    // Start is called before the first frame update
    void Start()
    {
        lastFrameMousePosition = Input.mousePosition;
    }

    // Update is called once per frame
    void Update()
    {
        bool mouseLeftDown = Input.GetMouseButtonDown((int)MouseButton.LeftMouse);
        if (mouseLeftDown)
        {
            lastFrameMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButton((int)MouseButton.LeftMouse))
        {
            Vector3 mouseDelta = lastFrameMousePosition - Input.mousePosition;

            //Debug.Log("mouseDelta = " + mouseDelta);

            lastFrameMousePosition = Input.mousePosition;
            Quaternion yRotate = Quaternion.AngleAxis(-mouseDelta.x, Vector3.up);
            transform.forward = yRotate * transform.forward;

            Quaternion xRotate = Quaternion.AngleAxis(mouseDelta.y, transform.right);
            transform.forward = xRotate * transform.forward;
        }
        else
        {
            //Debug.Log("alt key is up!");

            if (Input.GetKey(KeyCode.W))
            {
                transform.position += transform.forward * Time.deltaTime * moveSpeed;
            }
            else if (Input.GetKey(KeyCode.S))
            {
                transform.position -= transform.forward * Time.deltaTime * moveSpeed;
            }
        }

    }
}
