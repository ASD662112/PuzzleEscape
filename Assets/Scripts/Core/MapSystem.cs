using System.Collections.Generic;
using PuzzleEscape.Interaction;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 区域地图数据：从现有线索点推导房间名与楼层，记录玩家已探索的房间。
    /// </summary>
    public class MapSystem : MonoBehaviour
    {
        public class RoomInfo
        {
            public string Id;
            public string DisplayName;
            public int Floor;
            public Vector3 Center;
        }

        public static MapSystem Instance { get; private set; }

        private readonly List<RoomInfo> rooms = new List<RoomInfo>();
        private readonly HashSet<string> visited = new HashSet<string>();
        private float nextScan;

        public IReadOnlyList<RoomInfo> Rooms => rooms;
        public string CurrentRoomId { get; private set; }
        public RoomInfo CurrentRoom { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            BuildRooms();
            ScanPlayer(true);
        }

        private void Update()
        {
            if (Time.time < nextScan)
            {
                return;
            }

            nextScan = Time.time + 0.25f;
            ScanPlayer(false);
        }

        public bool IsVisited(string roomId)
        {
            return !string.IsNullOrEmpty(roomId) && visited.Contains(roomId);
        }

        public List<RoomInfo> GetRooms(int floor)
        {
            var result = new List<RoomInfo>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].Floor == floor)
                {
                    result.Add(rooms[i]);
                }
            }

            result.Sort(delegate(RoomInfo a, RoomInfo b)
            {
                int z = a.Center.z.CompareTo(b.Center.z);
                return z != 0 ? z : a.Center.x.CompareTo(b.Center.x);
            });
            return result;
        }

        public void CaptureSave(SaveGameData data)
        {
            if (data == null)
            {
                return;
            }

            data.VisitedRooms.Clear();
            data.VisitedRooms.AddRange(visited);
        }

        public void RestoreSave(SaveGameData data)
        {
            visited.Clear();
            if (data != null && data.VisitedRooms != null)
            {
                foreach (string id in data.VisitedRooms)
                {
                    visited.Add(id);
                }
            }
        }

        private void BuildRooms()
        {
            rooms.Clear();
            ClueInteractable[] clues = Object.FindObjectsOfType<ClueInteractable>(true);

            foreach (ClueInteractable clue in clues)
            {
                if (clue == null)
                {
                    continue;
                }

                string displayName = CleanName(clue.Title);
                if (string.IsNullOrEmpty(displayName))
                {
                    continue;
                }

                int floor = clue.transform.position.y >= 3.2f
                    ? 2
                    : (clue.transform.position.y <= -0.5f ? -1 : 1);

                string id = floor + "_" + displayName;
                bool exists = false;
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].Id == id)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                rooms.Add(new RoomInfo
                {
                    Id = id,
                    DisplayName = displayName,
                    Floor = floor,
                    Center = clue.transform.position
                });
            }
        }

        private void ScanPlayer(bool force)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null || rooms.Count == 0)
            {
                return;
            }

            Vector3 position = player.transform.position;
            RoomInfo nearest = null;
            float best = 5.0f;

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomInfo room = rooms[i];
                Vector3 flatPlayer = new Vector3(position.x, 0f, position.z);
                Vector3 flatRoom = new Vector3(room.Center.x, 0f, room.Center.z);
                float distance = Vector3.Distance(flatPlayer, flatRoom);

                int playerFloor = position.y >= 3.2f ? 2 : (position.y <= -0.5f ? -1 : 1);
                if (room.Floor != playerFloor)
                {
                    continue;
                }

                if (distance < best)
                {
                    best = distance;
                    nearest = room;
                }
            }

            if (nearest == null)
            {
                return;
            }

            if (force || CurrentRoomId != nearest.Id)
            {
                CurrentRoomId = nearest.Id;
                CurrentRoom = nearest;
            }

            visited.Add(nearest.Id);
        }

        private static string CleanName(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            string name = title;
            string[] suffixes =
            {
                "便签", "笔记", "清单", "记录", "说明", "纸条", "挂钟", "手记"
            };

            foreach (string suffix in suffixes)
            {
                if (name.EndsWith(suffix))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                }
            }

            name = name.Replace("里的", string.Empty);
            return name.Trim();
        }
    }
}
