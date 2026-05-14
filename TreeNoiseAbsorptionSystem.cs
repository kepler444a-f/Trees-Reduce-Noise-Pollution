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

        // Persistent array to avoid GC allocation spikes
        private NativeArray<int> m_DensityMap;

        private const float MAP_SIZE = 14336f;
        private const int TEXTURE_SIZE = 256;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_NoiseSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();

            m_TreeQuery = GetEntityQuery(
                ComponentType.ReadOnly<Tree>(),
                ComponentType.ReadOnly<Game.Objects.Transform>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
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
            int interval = math.max(1, Mod.m_Setting.UpdateInterval);
            if (m_FrameCounter % interval != 0) return;

            // Get noise map handle
            var noiseMap = m_NoiseSystem.GetMap(false, out JobHandle noiseDeps);

            // Job 1: Clear the existing density map
            var clearJob = new ClearDensityJob { m_Density = m_DensityMap };
            JobHandle clearHandle = clearJob.Schedule(m_DensityMap.Length, 64, Dependency);

            // Job 2: Count trees across all CPU cores (Atomic)
            var countJob = new CountTreesParallelJob
            {
                m_Density = m_DensityMap,
                m_MapSize = MAP_SIZE,
                m_Size = TEXTURE_SIZE
            };
            JobHandle countHandle = countJob.ScheduleParallel(m_TreeQuery, JobHandle.CombineDependencies(clearHandle, noiseDeps));

            // Job 3: Apply the noise reduction math
            var applyJob = new ApplyReductionJob
            {
                m_Noise = noiseMap,
                m_Density = m_DensityMap,
                m_Strength = Mod.m_Setting.TreeNoiseStrength,
                m_Mode = Mod.m_Setting.ReductionMode
            };

            Dependency = applyJob.Schedule(m_DensityMap.Length, 64, countHandle);
        }

        [BurstCompile]
        struct ClearDensityJob : IJobParallelFor
        {
            public NativeArray<int> m_Density;
            public void Execute(int index) => m_Density[index] = 0;
        }

        [BurstCompile]
        public partial struct CountTreesParallelJob : IJobEntity
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> m_Density;
            public float m_MapSize;
            public int m_Size;

            public unsafe void Execute(in Game.Objects.Transform transform)
            {
                float3 pos = transform.m_Position;
                float cellSize = m_MapSize / m_Size;

                int x = (int)math.floor((pos.x + m_MapSize * 0.5f) / cellSize);
                int z = (int)math.floor((pos.z + m_MapSize * 0.5f) / cellSize);

                if ((uint)x < m_Size && (uint)z < m_Size)
                {
                    int index = x + (z * m_Size);
                    // Optimized atomic addition for multithreading
                    Interlocked.Add(ref ((int*)m_Density.GetUnsafePtr())[index], 1);
                }
            }
        }

        [BurstCompile]
        struct ApplyReductionJob : IJobParallelFor
        {
            public NativeArray<NoisePollution> m_Noise;
            [ReadOnly] public NativeArray<int> m_Density;
            public int m_Strength;
            public int m_Mode;

            public void Execute(int index)
            {
                int treeCount = m_Density[index];
                if (treeCount <= 0) return;

                NoisePollution data = m_Noise[index];
                if (data.m_PollutionTemp <= 0) return;

                float reduction = (m_Mode == 1)
                    ? math.log2(treeCount + 1f) * m_Strength
                    : treeCount * m_Strength;

                int result = math.max(0, (int)data.m_PollutionTemp - (int)math.round(reduction));

                data.m_PollutionTemp = (short)math.clamp(result, 0, 32767);
                m_Noise[index] = data;
            }
        }
    }
}