using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed;
    public float health;
    public float maxHealth;
    public RuntimeAnimatorController[] animatorCon;

    int slotIndex;
    bool isLive;
    Rigidbody2D rigid;
    SpriteRenderer spriteR;
    SpriteRenderer shadowR;
    Animator anim;
    Collider2D coll;
    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriteR = GetComponent<SpriteRenderer>();
        shadowR = transform.GetChild(0).GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        coll = GetComponent<Collider2D>();
    }

    public void SetSlotIndex(int i) => slotIndex = i;

    private void LateUpdate()
    {
        if (!GameManager.instance.isLive) return;

        //update huong nhin cua nhan vat enemy ve phia target
        spriteR.flipX = EnemyManager.instance.GetFlipX(slotIndex);

        //update thu tu ve theo truc Y (thap hon = truoc), tinh tuong doi so voi player
        int order = DepthSort.ComputeOrder(transform.position.y, GameManager.instance.player.transform.position.y);
        spriteR.sortingOrder = order;
        shadowR.sortingOrder = order - 1;
    }
    private void OnEnable()
    {
        //spawn
        isLive = true;
        coll.enabled = true;
        rigid.simulated = true;
        anim.SetBool("Dead", false);
        health = maxHealth;
        slotIndex = EnemyManager.instance.Register(this);
    }
    private void OnDisable()
    {
        // EnemyManager may already be torn down (scene unload / exiting Play Mode) before this fires.
        if (EnemyManager.instance != null) EnemyManager.instance.Unregister(slotIndex);
    }
    public void Init(SpawnData data)
    {
        //gan thong tin animation, mau, toc do cho enemy
        anim.runtimeAnimatorController = animatorCon[data.spriteType];
        speed = data.speed;
        maxHealth = data.health;
        health = data.health;
        EnemyManager.instance.SetSpeed(slotIndex, speed);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Bullet") || !isLive)
        {
            return;
        }
        // khi cham vao bullet cua nguoi choi, enemy bi tru mau bang damage cua bullet
        health -= collision.GetComponent<Bullet>().damage;
        Vector2 knockDir = (Vector2)transform.position - (Vector2)GameManager.instance.player.transform.position;
        EnemyManager.instance.ApplyHit(slotIndex, knockDir);
        if (health <= 0)
        {
            isLive = false;
            EnemyManager.instance.SetLive(slotIndex, false);

            //khien khong the tuong tac voi object da chet
            coll.enabled = false;
            rigid.simulated = false;
            anim.SetBool("Dead", true);
            GameManager.instance.kill++;
            for (int i = 0; i < animatorCon.Length; i++)
            {
                if (anim.runtimeAnimatorController == animatorCon[i])
                {
                    switch (i)
                    {
                        case 0:
                        case 2:
                            GameManager.instance.GetExp();
                            break;
                        case 1:
                        case 3:
                            GameManager.instance.GetExp();
                            GameManager.instance.GetExp();
                            break;
                        case 4:
                            GameManager.instance.GetExp();
                            GameManager.instance.GetExp();
                            GameManager.instance.GetExp();
                            GameManager.instance.GetExp();
                            GameManager.instance.GetExp();
                            break;
                    }
                    break;
                }
            }
            
            if (GameManager.instance.isLive)    AudioManager.instance.PlaySfx(AudioManager.Sfx.Dead);


            /*    Dead();*/ // mau xuong 0, kich hoat event chet cua enemy
        }
        else {
            anim.SetTrigger("Hit");//khi mau chua xuong hong. chay anim get hit
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Hit);

        }
    }
    private void Dead()
    {

        //xu ly event chet cua enemy
        
        gameObject.SetActive(false);

    }
}
