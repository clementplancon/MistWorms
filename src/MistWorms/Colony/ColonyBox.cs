using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>The box worms are confined to (spec §8.4). Stored as centre and half size in the colony ZDO.</summary>
    internal static class ColonyBox
    {
        /// <summary>Margin added around the rooms of a mine (spec §8.4).</summary>
        public const float MineMargin = 2f;

        /// <summary>Half size of the box of a test colony (outside any dungeon).</summary>
        public static readonly Vector3 TestHalfSize = new Vector3(64f, 32f, 64f);

        public static void ForTest(Vector3 center, out Vector3 boxCenter, out Vector3 boxHalfSize)
        {
            boxCenter = center;
            boxHalfSize = TestHalfSize;
        }

        /// <summary>
        /// Union of the oriented boxes of the rooms below the generator, as an axis aligned box, plus <see cref="MineMargin"/>.
        /// A room is centred on its transform and sized by Room.m_size (decisions.md 1.2 line 9). Without any loaded room
        /// the generator's zone box is used.
        /// </summary>
        public static void ForDungeon(DungeonGenerator generator, out Vector3 boxCenter, out Vector3 boxHalfSize)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int rooms = 0;
            foreach (Room room in generator.GetComponentsInChildren<Room>())
            {
                Vector3 half = new Vector3(room.m_size.x, room.m_size.y, room.m_size.z) * 0.5f;
                WormMath.ExpandByObb(room.transform.position, room.transform.rotation, half, ref min, ref max);
                rooms++;
            }

            if (rooms == 0)
            {
                Vector3 c = generator.transform.TransformPoint(generator.m_zoneCenter);
                Vector3 h = generator.m_zoneSize * 0.5f;
                min = c - h;
                max = c + h;
            }

            Vector3 margin = Vector3.one * MineMargin;
            boxCenter = (min + max) * 0.5f;
            boxHalfSize = (max - min) * 0.5f + margin;
        }
    }
}
