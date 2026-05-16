using Game;
using Game.Common;
using Game.Objects;
using Game.Simulation;
using Game.Tools;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TreesReduceNoisePollution
{
    [BurstCompile]
    [UpdateAfter(typeof(NoisePollutionSystem))] 
    public partial class TreeNoiseAbsorptionSystem : GameSystemBase
    {
        private EntityQuery m_TreeQuery;
        private NoisePollutionSystem m_NoiseSystem;
        private int m_FrameCounter;
        private NativeArray<int> m_DensityMap;

        private const float MAP_SIZE = 14336f;
        private const int TEXTURE_SIZE = 128;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_NoiseSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();

            m_TreeQuery = GetEntityQuery(
                ComponentType.ReadOnly<Tree>(),
                ComponentType.ReadOnly<Game.Objects.Transform>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Game.Tools.Temp>()
            );

            m_DensityMap = new NativeArray<int>(TEXTURE_SIZE * TEXTURE_SIZE, Allocator.Persistent);
            RequireForUpdate(m_TreeQuery);
        }

        protected override void OnDestroy()
        {
            if (m_DensityMap.IsCreated) m_DensityMap.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (Mod.m_Setting == null || !Mod.m_Setting.ModEnabled) return;

            m_FrameCounter++;
            if (m_FrameCounter % Mod.m_Setting.UpdateInterval != 0) return;

            var noiseMap = m_NoiseSystem.GetMap(false, out var noiseDeps);

            var clearJob = new ClearDensityJob { m_Density = m_DensityMap };
            var clearHandle = clearJob.Schedule(Dependency);

            var countJob = new CountTreesJob
            {
                m_Density = m_DensityMap,
                m_MapSize = MAP_SIZE,
                m_Size = TEXTURE_SIZE
            };
            var countHandle = countJob.ScheduleParallel(m_TreeQuery, clearHandle);

            var applyJob = new ApplyReductionJob
            {
                m_Noise = noiseMap,
                m_Density = m_DensityMap,
                m_DensitySize = TEXTURE_SIZE,
                m_Strength = Mod.m_Setting.TreeNoiseStrength,
                m_Mode = Mod.m_Setting.ReductionMode,
                m_Radius = Mod.m_Setting.AbsorptionRadius // Passed setting here
            };

            JobHandle combinedDeps = JobHandle.CombineDependencies(countHandle, noiseDeps);
            Dependency = applyJob.Schedule(noiseMap.Length, 64, combinedDeps);
            
            m_NoiseSystem.AddWriter(Dependency);
        }

        [BurstCompile]
        public struct ClearDensityJob : IJob
        {
            public NativeArray<int> m_Density;
            
            public unsafe void Execute()
            {
                UnsafeUtility.MemClear(m_Density.GetUnsafePtr(), m_Density.Length * sizeof(int));
            }
        }

        [BurstCompile]
        public partial struct CountTreesJob : IJobEntity
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> m_Density;
            public float m_MapSize;
            public int m_Size;

            public unsafe void Execute(in Game.Objects.Transform transform, in Tree tree)
            {
                if ((tree.m_State & TreeState.Dead) != 0 || (tree.m_State & TreeState.Adult) == 0)
                    return;

                float3 pos = transform.m_Position;
                float cellSize = m_MapSize / m_Size;
                int x = (int)((pos.x + m_MapSize * 0.5f) / cellSize);
                int z = (int)((pos.z + m_MapSize * 0.5f) / cellSize);

                if ((uint)x < (uint)m_Size && (uint)z < (uint)m_Size)
                {
                    int index = x + (z * m_Size);
                    int* ptr = (int*)m_Density.GetUnsafePtr();
                    Interlocked.Increment(ref ptr[index]);
                }
            }
        }

        [BurstCompile]
        public struct ApplyReductionJob : IJobParallelFor
        {
            public NativeArray<NoisePollution> m_Noise;
            [ReadOnly] public NativeArray<int> m_Density;
            public int m_DensitySize;
            public int m_Strength;
            public int m_Mode;
            public int m_Radius;

            public void Execute(int i)
            {
                int noiseMapSize = (int)math.sqrt(m_Noise.Length); 
                
                int noiseX = i % noiseMapSize;
                int noiseZ = i / noiseMapSize;

                int centerDensityX = (int)((float)noiseX / noiseMapSize * m_DensitySize);
                int centerDensityZ = (int)((float)noiseZ / noiseMapSize * m_DensitySize);

                float effectiveTreeCount = 0f;

                // If Radius is 0, skip neighborhood kernel loop completely for speed
                if (m_Radius <= 0)
                {
                    int densityIndex = centerDensityX + (centerDensityZ * m_DensitySize);
                    effectiveTreeCount = m_Density[densityIndex];
                }
                else
                {
                    // Scan the 2D neighborhood bounding box
                    for (int offsetZ = -m_Radius; offsetZ <= m_Radius; offsetZ++)
                    {
                        for (int offsetX = -m_Radius; offsetX <= m_Radius; offsetX++)
                        {
                            int targetX = centerDensityX + offsetX;
                            int targetZ = centerDensityZ + offsetZ;

                            // Direct array boundary safety checking
                            if (targetX >= 0 && targetX < m_DensitySize && targetZ >= 0 && targetZ < m_DensitySize)
                            {
                                int neighborIndex = targetX + (targetZ * m_DensitySize);
                                int rawCount = m_Density[neighborIndex];

                                if (rawCount > 0)
                                {
                                    // Calculate Euclidean distance from center noise cell to sample cell
                                    float distance = math.sqrt(offsetX * offsetX + offsetZ * offsetZ);

                                    if (distance <= m_Radius)
                                    {
                                        // Linear falloff: Closer cells provide higher strength modifiers
                                        float weight = 1.0f - (distance / (m_Radius + 1f));
                                        effectiveTreeCount += rawCount * weight;
                                    }
                                    // Extreme edge protection: exact bounds fallback if diagonal float math slips past
                                    else if (offsetX == 0 || offsetZ == 0)
                                    {
                                        float weight = 1.0f - (distance / (m_Radius + 1f));
                                        effectiveTreeCount += rawCount * math.max(0f, weight);
                                    }
                                }
                            }
                        }
                    }
                }

                if (effectiveTreeCount <= 0f) return;

                NoisePollution data = m_Noise[i];
                
                // Mathematical logic matches original parameters but uses weighted float counts
                float reduction = (m_Mode == 1)
                    ? math.log2(effectiveTreeCount + 1f) * m_Strength
                    : effectiveTreeCount * (m_Strength / 10f);

                int currentPollution = (int)data.m_PollutionTemp;
                data.m_PollutionTemp = (short)math.max(0, currentPollution - (int)reduction);
                
                m_Noise[i] = data;
            }
        }
    }
}