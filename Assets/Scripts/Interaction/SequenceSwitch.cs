using PuzzleEscape.Audio;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 实验室里的单个顺序开关。
    /// </summary>
    public class SequenceSwitch : Interactable
    {
        public LabSequencePuzzle Puzzle;
        public int SwitchIndex = 1;
        public string SwitchLabel = "红色开关";
        public Transform HandlePivot;
        public Renderer Indicator;
        public Color ActiveColor = new Color(0.4f, 1f, 0.6f, 1f);
        public float PressedAngle = -55f;

        private bool activated;

        public override string Prompt => Puzzle != null && Puzzle.IsSolved ? "机关（已完成）" : "启动" + SwitchLabel;
        public override bool CanInteract => Puzzle != null && !Puzzle.IsSolved;

        public override void Interact(PlayerInteractor interactor)
        {
            Puzzle?.Press(SwitchIndex, this);
        }

        public void SetActivated(bool value)
        {
            activated = value;

            if (HandlePivot != null)
            {
                HandlePivot.localRotation = Quaternion.Euler(0f, 0f, value ? PressedAngle : 0f);
            }

            if (Indicator != null)
            {
                Material material = Indicator.material;
                Color color = value ? ActiveColor : new Color(0.75f, 0.25f, 0.25f, 1f);
                material.color = color;

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.8f);
                }
            }

            if (value)
            {
                AudioManager.Instance?.PlayClick();
            }
        }
    }
}
