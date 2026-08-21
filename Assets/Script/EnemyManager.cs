using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager instance;

    public float knockbackSpeed = 6f;
    public float knockbackDuration = 0.15f;
    public float knockbackImmuneDuration = 0.5f; // after being knocked back, ignore further knockback for this long
    public float hitLockDuration = 0.17f; // matches the Hit clip length (~0.083-0.167s across the 5 variants)
    public float separationRadius = 0.6f; // roughly the enemy collider width; pushes overlapping enemies apart (Kinematic bodies don't get this from physics)
    public float separationStrength = 8f;
    public float repositionBoundsHalf = 10f; // matches the old "Area" trigger's 20x20 box; enemies straggling past this get moved to a Spawner point
    public float repositionJitter = 3f;
    public Spawner spawner;

    NativeList<float> speeds;
    NativeList<float> hitTimers;
    NativeList<float> knockbackTimers;
    NativeList<float> knockbackImmuneTimers;
    NativeList<float2> knockbackDirs;
    NativeList<bool> flipX;
    NativeList<bool> isLive;
    TransformAccessArray transforms;
    List<Enemy> owners;

    JobHandle handle;

    void Awake()
    {
        instance = this;
        if (spawner == null) spawner = FindObjectOfType<Spawner>();
        speeds = new NativeList<float>(256, Allocator.Persistent);
        hitTimers = new NativeList<float>(256, Allocator.Persistent);
        knockbackTimers = new NativeList<float>(256, Allocator.Persistent);
        knockbackImmuneTimers = new NativeList<float>(256, Allocator.Persistent);
        knockbackDirs = new NativeList<float2>(256, Allocator.Persistent);
        flipX = new NativeList<bool>(256, Allocator.Persistent);
        isLive = new NativeList<bool>(256, Allocator.Persistent);
        transforms = new TransformAccessArray(256);
        owners = new List<Enemy>();
    }

    void OnDestroy()
    {
        instance = null;
        speeds.Dispose();
        hitTimers.Dispose();
        knockbackTimers.Dispose();
        knockbackImmuneTimers.Dispose();
        knockbackDirs.Dispose();
        flipX.Dispose();
        isLive.Dispose();
        transforms.Dispose();
    }

    public int Register(Enemy enemy)
    {
        speeds.Add(enemy.speed);
        hitTimers.Add(0f);
        knockbackTimers.Add(0f);
        knockbackImmuneTimers.Add(0f);
        knockbackDirs.Add(float2.zero);
        flipX.Add(false);
        isLive.Add(true);
        transforms.Add(enemy.transform);
        owners.Add(enemy);
        return transforms.length - 1;
    }

    public void Unregister(int index)
    {
        int last = transforms.length - 1;
        speeds.RemoveAtSwapBack(index);
        hitTimers.RemoveAtSwapBack(index);
        knockbackTimers.RemoveAtSwapBack(index);
        knockbackImmuneTimers.RemoveAtSwapBack(index);
        knockbackDirs.RemoveAtSwapBack(index);
        flipX.RemoveAtSwapBack(index);
        isLive.RemoveAtSwapBack(index);
        transforms.RemoveAtSwapBack(index);
        owners[index] = owners[last];
        owners.RemoveAt(last);
        if (index < owners.Count) owners[index].SetSlotIndex(index);
    }

    public void SetSpeed(int index, float speed) => speeds[index] = speed;

    public void SetLive(int index, bool live) => isLive[index] = live;

    public void ApplyHit(int index, Vector2 knockbackDirection)
    {
        hitTimers[index] = hitLockDuration;
        if (knockbackImmuneTimers[index] <= 0f)
        {
            knockbackTimers[index] = knockbackDuration;
            knockbackDirs[index] = math.normalizesafe(new float2(knockbackDirection.x, knockbackDirection.y));
            knockbackImmuneTimers[index] = knockbackImmuneDuration;
        }
    }

    public bool GetFlipX(int index) => flipX[index];

    void FixedUpdate()
    {
        if (!GameManager.instance.isLive || transforms.length == 0) return;

        Vector3 p = GameManager.instance.player.transform.position;
        int count = transforms.length;
        var positions = new NativeArray<float2>(count, Allocator.TempJob);

        var copyJob = new CopyPositionsJob { positions = positions };
        handle = copyJob.Schedule(transforms);
        handle.Complete();

        // Spawner's points (skipping its own transform at index 0, same as Spawner.Spawn()) double
        // as the recycle destinations for enemies that fall too far behind the player.
        Transform[] spPoints = spawner != null ? spawner.spawnPoint : null;
        int spCount = spPoints != null && spPoints.Length > 1 ? spPoints.Length - 1 : 0;
        var spawnPoints = new NativeArray<float2>(spCount, Allocator.TempJob);
        for (int i = 0; i < spCount; i++)
        {
            Vector3 sp = spPoints[i + 1].position;
            spawnPoints[i] = new float2(sp.x, sp.y);
        }

        var job = new EnemyMovementJob
        {
            targetPos = new float2(p.x, p.y),
            deltaTime = Time.fixedDeltaTime,
            speeds = speeds.AsArray(),
            hitTimers = hitTimers.AsArray(),
            knockbackTimers = knockbackTimers.AsArray(),
            knockbackImmuneTimers = knockbackImmuneTimers.AsArray(),
            knockbackDirs = knockbackDirs.AsArray(),
            flipX = flipX.AsArray(),
            isLive = isLive.AsArray(),
            positions = positions,
            spawnPoints = spawnPoints,
            knockbackSpeed = knockbackSpeed,
            separationRadius = separationRadius,
            separationStrength = separationStrength,
            repositionBoundsHalf = repositionBoundsHalf,
            repositionJitter = repositionJitter,
            seed = (uint)(UnityEngine.Time.frameCount * 9781 + 1),
        };
        handle = job.Schedule(transforms);
        handle.Complete();
        positions.Dispose();
        spawnPoints.Dispose();
        Physics2D.SyncTransforms();
    }

    [BurstCompile]
    struct CopyPositionsJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<float2> positions;

        public void Execute(int index, TransformAccess transform)
        {
            positions[index] = new float2(transform.position.x, transform.position.y);
        }
    }

    [BurstCompile]
    struct EnemyMovementJob : IJobParallelForTransform
    {
        public float2 targetPos;
        public float deltaTime;
        public float knockbackSpeed;
        public float separationRadius;
        public float separationStrength;
        public float repositionBoundsHalf;
        public float repositionJitter;
        public uint seed;
        public NativeArray<float> speeds;
        public NativeArray<float> hitTimers;
        public NativeArray<float> knockbackTimers;
        public NativeArray<float> knockbackImmuneTimers;
        public NativeArray<float2> knockbackDirs;
        public NativeArray<bool> flipX;
        [ReadOnly] public NativeArray<bool> isLive;
        [ReadOnly] public NativeArray<float2> positions;
        [ReadOnly] public NativeArray<float2> spawnPoints;

        public void Execute(int index, TransformAccess transform)
        {
            float2 pos = new float2(transform.position.x, transform.position.y);

            if (knockbackImmuneTimers[index] > 0f)
            {
                knockbackImmuneTimers[index] -= deltaTime;
            }

            if (knockbackTimers[index] > 0f)
            {
                knockbackTimers[index] -= deltaTime;
                pos += knockbackDirs[index] * knockbackSpeed * deltaTime;
                transform.position = new float3(pos, 0f);
                return;
            }

            if (!isLive[index]) return;

            // Enemy fell too far behind the player (used to be detected via an "Area" trigger +
            // Reposition component); recycle it to one of the Spawner's own spawn points instead.
            float2 toPlayer = targetPos - pos;
            if (math.abs(toPlayer.x) > repositionBoundsHalf || math.abs(toPlayer.y) > repositionBoundsHalf)
            {
                var rng = new Unity.Mathematics.Random((seed + (uint)index * 747796405u + 2891336453u) | 1u);
                float2 jitter = rng.NextFloat2(-repositionJitter, repositionJitter);
                pos = spawnPoints.Length > 0
                    ? spawnPoints[rng.NextInt(0, spawnPoints.Length)] + jitter
                    : targetPos + toPlayer + jitter;
                transform.position = new float3(pos, 0f);
                knockbackTimers[index] = 0f;
                hitTimers[index] = 0f;
                return;
            }

            // Kinematic bodies never get pushed apart by the physics solver, so overlapping
            // enemies need an explicit separation term here (boid-style), otherwise they stack.
            float2 separation = float2.zero;
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == index || !isLive[i]) continue;
                float2 diff = pos - positions[i];
                float d = math.length(diff);
                if (d > 0f && d < separationRadius)
                {
                    separation += (diff / d) * (separationRadius - d);
                }
            }
            pos += separation * separationStrength * deltaTime;

            if (hitTimers[index] > 0f)
            {
                hitTimers[index] -= deltaTime;
                transform.position = new float3(pos, 0f);
                return;
            }

            float2 dir = targetPos - pos;
            float len = math.length(dir);
            float2 dirN = len > 1e-5f ? dir / len : float2.zero;
            pos += dirN * speeds[index] * deltaTime;
            transform.position = new float3(pos, 0f);
            flipX[index] = targetPos.x < pos.x;
        }
    }
}
