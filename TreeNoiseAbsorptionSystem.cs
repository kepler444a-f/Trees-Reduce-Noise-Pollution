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

            // m_TreeQuery looks for all adult living trees, excluding deleted and preview (Temp) ones
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

            // Get the noise map with write access (false = writeable)
            var noiseMap = m_NoiseSystem.GetMap(false, out var noiseDeps);

            // 1. Reset Map: Uses Fast Burst MemClear
            var clearJob = new ClearDensityJob { m_Density = m_DensityMap };
            var clearHandle = clearJob.Schedule(Dependency);

            // 2. Count Trees: Parallelized using Atomic Atomics
            var countJob = new CountTreesJob
            {
                m_Density = m_DensityMap,
                m_MapSize = MAP_SIZE,
                m_Size = TEXTURE_SIZE
            };
            var countHandle = countJob.ScheduleParallel(m_TreeQuery, clearHandle);

            // 3. Apply Reduction: Distributed across all CPU cores
            var applyJob = new ApplyReductionJob
            {
                m_Noise = noiseMap,
                m_Density = m_DensityMap,
                m_Strength = Mod.m_Setting.TreeNoiseStrength,
                m_Mode = Mod.m_Setting.ReductionMode
            };

            JobHandle combinedDeps = JobHandle.CombineDependencies(countHandle, noiseDeps);
            
            // Scheduling as ParallelFor with batch size of 64 for smoothness
            Dependency = applyJob.Schedule(noiseMap.Length, 64, combinedDeps);
            
            // Signal to the engine that we are modifying the noise data
            m_NoiseSystem.AddReader(Dependency);
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
                // Logic: Only adult, living trees absorb noise
                if ((tree.m_State & TreeState.Dead) != 0 || (tree.m_State & TreeState.Adult) == 0)
                    return;

                float3 pos = transform.m_Position;
                float cellSize = m_MapSize / m_Size;
                int x = (int)((pos.x + m_MapSize * 0.5f) / cellSize);
                int z = (int)((pos.z + m_MapSize * 0.5f) / cellSize);

                if ((uint)x < (uint)m_Size && (uint)z < (uint)m_Size)
                {
                    int index = x + (z * m_Size);
                    
                    // ATOMIC: Thread-safe incrementing for high performance
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
            public int m_Strength;
            public int m_Mode;

            public void Execute(int i)
            {
                int treeCount = m_Density[i % m_Density.Length];
                if (treeCount <= 0) return;

                NoisePollution data = m_Noise[i];
                
                // Mode 1: Logarithmic | Mode 0: Linear
                float reduction = (m_Mode == 1)
                    ? math.log2(treeCount + 1f) * m_Strength
                    : treeCount * (m_Strength / 10f);

                int currentPollution = (int)data.m_PollutionTemp;
                data.m_PollutionTemp = (short)math.max(0, currentPollution - (int)reduction);
                
                m_Noise[i] = data;
            }
        }
    }
}