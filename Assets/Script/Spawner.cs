using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public Transform[] spawnPoint;
    public SpawnData[] spawnData;
    public float levelTime;
    public int enemyLevel;
    public float jitterSpawner;

    float timer;
    int level;
    private void Awake()
    {
        spawnPoint = GetComponentsInChildren<Transform>();
        levelTime = GameManager.instance.maxGameTime / spawnData.Length;
    }
    private void Update()
    {
        if (!GameManager.instance.isLive) return;

        //dieu kien de spawn enemy
        timer += Time.deltaTime;
        level = Mathf.Min(Mathf.FloorToInt((GameManager.instance.gameTime % 60f) / 10f), spawnData.Length - 1);
        if (GameManager.instance.gameTime >= 50f && enemyLevel < 2)
        {
            for (int i = 0; i < spawnData.Length; i++)
            {
                spawnData[i].health += 10 * (GameManager.instance.gameLevel - 1);
            }
            enemyLevel++;
        }        
        if (GameManager.instance.gameTime >= 100f && enemyLevel < 3)
        {
            for (int i = 0; i < spawnData.Length; i++)
            {
                spawnData[i].health += 10 * (GameManager.instance.gameLevel - 1);
            }
            enemyLevel++;

        }
        if (GameManager.instance.gameTime >= 200f && enemyLevel < 4)
        {
            for (int i = 0; i < spawnData.Length; i++)
            {
                spawnData[i].health +=10 + 10 * (GameManager.instance.gameLevel - 1);
            }
            enemyLevel++;

        }
        if (timer >= (spawnData[level].spawnTime))
        {
            timer = 0;
            Spawn();
        }

    }
    void Spawn()
    {
        //lenh spawn enemy 
        GameObject enemy = GameManager.instance.pool.Get(0);
        enemy.transform.position = spawnPoint[Random.Range(1, spawnPoint.Length)].position + Random.insideUnitSphere * jitterSpawner;
        enemy.GetComponent<Enemy>().Init(spawnData[level]);
    }
}

[System.Serializable]
public class SpawnData
{
    public float spawnTime;
    public int spriteType;
    public int health;
    public float speed;

}
