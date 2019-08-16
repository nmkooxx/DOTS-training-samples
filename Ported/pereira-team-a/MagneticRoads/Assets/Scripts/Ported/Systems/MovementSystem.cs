﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


public class MovementSystem : JobComponentSystem
{
    private EntityQuery query;
    protected override void OnCreate()
    {
        query = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadWrite<LocalToWorld>(), ComponentType.ReadWrite<Translation>(), ComponentType.ReadWrite<Rotation>(), ComponentType.ReadWrite<SplineComponent>() },
            None = new[] { ComponentType.ReadOnly<ReachedEndOfSplineComponent>() }
        });
    }

    // TODO: Smoothly move along a spline with rotation
    // TODO: Write the LocalToWorld matrix here

    [BurstCompile]
    struct MoveJob : IJobForEach<LocalToWorld, Translation, Rotation, SplineComponent>
    {
        public float deltaTime;
        public void Execute(ref LocalToWorld localToWorld, ref Translation translation, ref Rotation rotation, ref SplineComponent trackSplineComponent)
        {
            //velocity based on if we are in an intersection or not
            float velocity = trackSplineComponent.IsInsideIntersection ? 0.3f : 1.0f;

            //here we calculate the t
            float dist = math.distance(trackSplineComponent.Spline.EndPosition, trackSplineComponent.Spline.StartPosition);
            var moveDisplacement = (deltaTime * velocity) / dist;
            var t = math.clamp(trackSplineComponent.t + moveDisplacement, 0, 1);

            //If we are inside a tempSpline, just interpolate to pass thorugh it
            if (trackSplineComponent.IsInsideIntersection)
            {
                translation.Value = translation.Value + math.normalize(trackSplineComponent.Spline.EndPosition - translation.Value) * deltaTime * velocity;
                rotation.Value = math.slerp(rotation.Value, quaternion.LookRotationSafe(trackSplineComponent.Spline.EndPosition - translation.Value, trackSplineComponent.Spline.StartNormal), trackSplineComponent.t);

                localToWorld = new LocalToWorld
                {
                    Value = float4x4.TRS(
                        translation.Value,
                        rotation.Value,
                        new float3(1.0f, 1.0f, 1.0f))
                };
                trackSplineComponent.t += deltaTime * velocity;

                return;
            }

            //if not, process to the full movement across the spline
            float3 pos = EvaluateCurve(trackSplineComponent.t, trackSplineComponent);

            //Calculating up vector
            quaternion fromTo = Quaternion.FromToRotation(trackSplineComponent.Spline.StartNormal, trackSplineComponent.Spline.EndNormal);
            float smoothT = math.smoothstep(0f, 1f, t * 1.02f - .01f);
            float3 up = math.mul(math.slerp(quaternion.identity, fromTo, smoothT), trackSplineComponent.Spline.StartNormal);

            translation.Value = pos + math.normalize(up) * 0.015f;
            rotation.Value = math.slerp(rotation.Value, quaternion.LookRotationSafe(trackSplineComponent.Spline.EndPosition - translation.Value, up), trackSplineComponent.t);

            localToWorld = new LocalToWorld
            {
                Value = float4x4.TRS(
                        translation.Value,
                        rotation.Value,
                        new float3(1.0f, 1.0f, 1.0f))
            };

            trackSplineComponent.t = t;
        }
    }

    public static float3 EvaluateCurve(float t, SplineComponent splineComponent)
    {
        t = math.clamp(t, 0, 1);
        return splineComponent.Spline.StartPosition * (1f - t) * (1f - t) * (1f - t) + 3f * splineComponent.Spline.Anchor1 * (1f - t) * (1f - t) * t + 3f * splineComponent.Spline.Anchor2 * (1f - t) * t * t + splineComponent.Spline.EndPosition * t * t * t;
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!RoadGenerator.ready || !RoadGenerator.useECS)
            return inputDeps;

        var job = new MoveJob
        {
            deltaTime = Time.deltaTime
        };
        return job.Schedule(query, inputDeps);
    }
}