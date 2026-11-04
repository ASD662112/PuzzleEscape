using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleEscape.Core
{
    [Serializable]
    public class ItemPointState
    {
        public string Path;
        public string ItemId;
        public bool HasSpawned;
        public bool Active;
        public bool SpawnOnStart;
    }

    [Serializable]
    public class ContainerState
    {
        public string Path;
        public bool Opened;
    }

    [Serializable]
    public class DrawerState
    {
        public string Path;
        public bool Opened;
    }

    [Serializable]
    public class PuzzleState
    {
        public string Path;
        public bool Solved;
    }

    [Serializable]
    public class HatchState
    {
        public string Path;
        public bool Opened;
    }

    [Serializable]
    public class GateState
    {
        public string Path;
        public bool Unlocked;
    }

    [Serializable]
    public class CaseEntryState
    {
        public string Id;
        public string Title;
        public string Body;
        public string Category;
    }

    [Serializable]
    public class DroppedItemState
    {
        public string ItemId;
        public Vector3 Position;
        public Vector3 Euler;
        public bool Settled;
    }

    [Serializable]
    public class SaveGameData
    {
        public int Version = 1;
        public string MapId;
        public float Elapsed;
        public bool Started;

        public bool DeskNoteRead;
        public bool KeyPicked;
        public bool DrawerOpened;
        public bool ClockRead;
        public bool PowerOn;
        public int InstalledFuses;
        public bool BasementKeyPicked;
        public bool LanternPicked;
        public bool BasementOpened;
        public bool ElevatorRepaired;
        public bool ElevatorUsed;
        public bool ArchiveUnlocked;
        public bool LabSolved;
        public bool FinalGateUnlocked;
        public bool SealSunCollected;
        public bool SealMoonCollected;
        public bool SealStarCollected;
        public bool SealShadowCollected;

        public Vector3 PlayerPosition;
        public float PlayerYaw;
        public float PlayerPitch;

        public List<string> InventoryItems = new List<string>();
        public List<ItemPointState> ItemPoints = new List<ItemPointState>();
        public List<ContainerState> Containers = new List<ContainerState>();
        public List<DrawerState> Drawers = new List<DrawerState>();
        public List<PuzzleState> Keypads = new List<PuzzleState>();
        public List<PuzzleState> LabPuzzles = new List<PuzzleState>();
        public List<HatchState> Hatches = new List<HatchState>();
        public List<GateState> Gates = new List<GateState>();
        public List<DroppedItemState> DroppedItems = new List<DroppedItemState>();

        public List<CaseEntryState> CaseEntries = new List<CaseEntryState>();
        public List<string> CaseNoteIds = new List<string>();
        public List<string> CaseSecretIds = new List<string>();
        public List<string> CasePuzzleIds = new List<string>();
        public bool CaseHintsUsed;

        public List<string> VisitedRooms = new List<string>();
    }
}
