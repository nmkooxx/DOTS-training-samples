﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

public struct UpscaleComponent : IComponentData
{
    public float targetScale;
}

public class SpawnerSystem : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer();
        float deltaTime = Time.DeltaTime;

        Entities.ForEach((ref SpawnerComponent spawner) =>
        {
            spawner.timeToNextSpawn -= deltaTime;
            while (spawner.timeToNextSpawn < 0)
            {
                var x = spawner.random.NextFloat(-spawner.extend.x, spawner.extend.x) / 2f;
                var y = spawner.random.NextFloat(-spawner.extend.y, spawner.extend.y) / 2f;
                var z = spawner.random.NextFloat(-spawner.extend.z, spawner.extend.z) / 2f;
                //var y = spawner.random.NextInt(0, rowCount - 1);
                //var z = spawner.random.NextInt(0, rowCount - 1);
                var entity = commandBuffer.Instantiate(spawner.spawnEntity);
                var c = spawner.center;
                commandBuffer.SetComponent(entity, new Translation { Value = c + new float3(x, y, z) });
                var scale = spawner.random.NextFloat(spawner.scaleRange.x, spawner.scaleRange.y);
                commandBuffer.AddComponent(entity, new Scale() { Value = 0f });
                commandBuffer.AddComponent(entity, new Velocity() { Value = spawner.velocity });
                commandBuffer.AddComponent(entity, new UpscaleComponent() { targetScale = scale }) ;
                spawner.timeToNextSpawn += 1f / spawner.frequency;
            }

        }).Run();

        return inputDependencies;
    }

}

public class UpscalerSystem : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer();
        float deltaTime = Time.DeltaTime;

        Entities.ForEach((ref Entity entity, ref UpscaleComponent scaler, ref Scale scale) =>
        {
            var newScale = scale.Value + 1f * deltaTime;
            if (newScale > scaler.targetScale)
            {
                newScale = scaler.targetScale;
                commandBuffer.RemoveComponent(entity, typeof(UpscaleComponent));
            }
            scale.Value = newScale;

        }).Run();

        return inputDependencies;
    }
}