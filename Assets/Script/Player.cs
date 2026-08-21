using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public Vector2 inputVec;
    public float speed;
    public Scanner scanner;
    public RuntimeAnimatorController[] animCon;

    Rigidbody2D rigid2D;
    SpriteRenderer spriteR;
    SpriteRenderer shadowR;
    Animator anim;
    private void Awake()
    {
        rigid2D = GetComponent<Rigidbody2D>();
        spriteR = GetComponent<SpriteRenderer>();
        shadowR = transform.GetChild(0).GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        scanner = GetComponent<Scanner>();
    }

    private void OnEnable()
    {
        speed *= Character.Speed;
        anim.runtimeAnimatorController = animCon[GameManager.instance.playerId];
    }

    void Update()
    {
        if (!GameManager.instance.isLive) return;

        //update vector dieu khien
/*        inputVec.x = Input.GetAxisRaw("Horizontal");
        inputVec.y = Input.GetAxisRaw("Vertical");*/

    }
    private void FixedUpdate()
    {
        if (!GameManager.instance.isLive) return;


        //update vi tri dieu khien
        Vector2 nextVec = inputVec.normalized * speed * Time.fixedDeltaTime;
        rigid2D.MovePosition(rigid2D.position + nextVec);
    }
    private void LateUpdate()
    {
        //update toc do
        anim.SetFloat("Speed", inputVec.magnitude);

        //update huong nhin cua nhan vat
        if (inputVec.x != 0)
        {
            spriteR.flipX = inputVec.x < 0;
        }

        //update thu tu ve theo truc Y, dung chung cong thuc voi Enemy de xen ke dung
        int order = DepthSort.ComputeOrder(transform.position.y, transform.position.y);
        spriteR.sortingOrder = order;
        shadowR.sortingOrder = order - 1;
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!GameManager.instance.isLive) return;
        GameManager.instance.health -= Time.deltaTime * 10;
        if (GameManager.instance.health < 0)
        {
            for (int i = 2; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);

            }
            anim.SetTrigger("Dead");
            GameManager.instance.GameOver();
        }
    }
   void OnMove(InputValue value)
    {
        inputVec = value.Get<Vector2>();
    }
}