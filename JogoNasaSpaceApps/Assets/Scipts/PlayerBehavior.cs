using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBehavior : MonoBehaviour
{
    public float PlayerSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Opcional: se o Player tiver um Rigidbody2D, o movimento usa a física (respeita colisões)
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        ReadInput();

        // Sem Rigidbody2D: move direto pelo Transform
        if (rb == null)
            PlayerMovement();
    }

    void FixedUpdate()
    {
        // Com Rigidbody2D: move pela física
        if (rb != null)
            rb.linearVelocity = moveInput * PlayerSpeed;
    }

    void ReadInput()
    {
        Vector2 input = Vector2.zero;
        Keyboard kb = Keyboard.current;

        if (kb != null)
        {
            if (kb.wKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed) input.x -= 1;
        }

        // Normaliza para não andar mais rápido na diagonal
        moveInput = input.normalized;
    }

    void PlayerMovement()
    {
        transform.Translate(moveInput * PlayerSpeed * Time.deltaTime, Space.World);
    }
}
