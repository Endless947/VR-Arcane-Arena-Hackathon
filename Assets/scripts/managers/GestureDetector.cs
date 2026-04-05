using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using VRArcaneArena.DataStructures;

namespace VRArcaneArena.Managers
{
    /// <summary>
    /// Converts held hand poses to gesture tokens and resolves spells through a trie.
    /// Uses XR Hands subsystem (OpenXR) — NOT OVRHand.
    /// </summary>
    public sealed class GestureDetector : MonoBehaviour
    {
        public SpellTrie spellTrie;
        public float poseHoldDuration = 0.3f;

        public UnityEvent<string> onSpellCast;
        public UnityEvent onInvalidGesture;
        public UnityEvent<List<string>> onReachableSpellsUpdated;

        private XRHandSubsystem _handSubsystem;
        private string _currentPose;
        private float _poseHoldTimer;
        private string _lastFiredPose;

        public void Awake()
        {
            spellTrie = new SpellTrie();
            spellTrie.LoadDefaultSpells();

            if (onSpellCast == null)           onSpellCast = new UnityEvent<string>();
            if (onInvalidGesture == null)      onInvalidGesture = new UnityEvent();
            if (onReachableSpellsUpdated == null) onReachableSpellsUpdated = new UnityEvent<List<string>>();

            _currentPose = string.Empty;
            _lastFiredPose = string.Empty;
            _poseHoldTimer = 0f;
        }

        public void Start()
        {
            // Get XR Hands subsystem — works with OpenXR on Quest 2
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
                _handSubsystem = subsystems[0];
            else
                Debug.LogWarning("GestureDetector: XRHandSubsystem not found. Keyboard fallback active.");
        }

        public void Update()
        {
            // Keyboard shortcuts — always work in Editor and as headset fallback
            if (Input.GetKeyDown(KeyCode.Space))      { onSpellCast.Invoke("Fireball");       return; }
            if (Input.GetKeyDown(KeyCode.Alpha1))     { onSpellCast.Invoke("Blizzard");        return; }
            if (Input.GetKeyDown(KeyCode.Alpha2))     { onSpellCast.Invoke("Lightning Bolt");  return; }
            if (Input.GetKeyDown(KeyCode.Alpha3))     { onSpellCast.Invoke("Arcane Shield");   return; }
            if (Input.GetKeyDown(KeyCode.Alpha4))     { onSpellCast.Invoke("Meteor Strike");   return; }
            if (Input.GetKeyDown(KeyCode.Alpha5))     { onSpellCast.Invoke("Gravity Well");    return; }
            if (Input.GetKeyDown(KeyCode.Alpha6))     { onSpellCast.Invoke("Frost Nova");      return; }
            if (Input.GetKeyDown(KeyCode.Alpha7))     { onSpellCast.Invoke("Void Blast");      return; }

            if (_handSubsystem == null || !_handSubsystem.running) return;

            var detectedPose = DetectCurrentPose();

            if (string.IsNullOrEmpty(detectedPose))
            {
                _currentPose = string.Empty;
                _poseHoldTimer = 0f;
                return;
            }

            if (detectedPose == _currentPose)
            {
                _poseHoldTimer += Time.deltaTime;
            }
            else
            {
                _currentPose = detectedPose;
                _poseHoldTimer = 0f;
            }

            if (_poseHoldTimer < poseHoldDuration) return;

            // Avoid re-firing the same held pose
            if (_currentPose == _lastFiredPose) return;

            ProcessGestureToken(_currentPose[0]);
            _lastFiredPose = _currentPose;
            _poseHoldTimer = 0f;
        }

        public void ProcessGestureToken(char token)
        {
            var spell = spellTrie.Traverse(token);

            if (!spellTrie.IsValidPrefix())
            {
                onInvalidGesture.Invoke();
                ResetGesture();
                return;
            }

            if (!string.IsNullOrEmpty(spell))
            {
                onSpellCast.Invoke(spell);
                ResetGesture();
                return;
            }

            onReachableSpellsUpdated.Invoke(spellTrie.GetReachableSpells());
        }

        /// <summary>
        /// Detects the current hand pose using XR Hands joint positions.
        /// Uses right hand. Returns "F", "P", "O", "S", or empty string.
        /// </summary>
        private string DetectCurrentPose()
        {
            var hand = _handSubsystem.rightHand;
            if (!hand.isTracked) return string.Empty;

            bool indexExtended  = IsFingerExtended(hand, XRHandJointID.IndexTip,  XRHandJointID.IndexProximal);
            bool middleExtended = IsFingerExtended(hand, XRHandJointID.MiddleTip, XRHandJointID.MiddleProximal);
            bool ringExtended   = IsFingerExtended(hand, XRHandJointID.RingTip,   XRHandJointID.RingProximal);
            bool pinkyExtended  = IsFingerExtended(hand, XRHandJointID.LittleTip, XRHandJointID.LittleProximal);
            bool thumbExtended  = IsFingerExtended(hand, XRHandJointID.ThumbTip,  XRHandJointID.ThumbProximal);

            int extendedCount = (indexExtended ? 1 : 0) + (middleExtended ? 1 : 0)
                              + (ringExtended  ? 1 : 0) + (pinkyExtended  ? 1 : 0);

            // Fist — all fingers curled
            if (extendedCount == 0) return "F";

            // Point — only index extended
            if (indexExtended && !middleExtended && !ringExtended && !pinkyExtended) return "P";

            // Spread — all fingers + thumb extended
            if (extendedCount >= 4 && thumbExtended) return "S";

            // Open Palm — 3-4 fingers extended
            if (extendedCount >= 3) return "O";

            return string.Empty;
        }

        /// <summary>
        /// Compares tip-to-wrist distance vs proximal-to-wrist distance.
        /// Tip further from wrist than proximal = finger is extended.
        /// </summary>
        private bool IsFingerExtended(XRHand hand, XRHandJointID tipID, XRHandJointID proximalID)
        {
            if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wristPose))   return false;
            if (!hand.GetJoint(tipID).TryGetPose(out Pose tipPose))                   return false;
            if (!hand.GetJoint(proximalID).TryGetPose(out Pose proxPose))             return false;

            float tipDist  = Vector3.Distance(tipPose.position,  wristPose.position);
            float proxDist = Vector3.Distance(proxPose.position, wristPose.position);

            return tipDist > proxDist * 1.2f;
        }

        public void ResetGesture()
        {
            spellTrie.Reset();
            _lastFiredPose = string.Empty;
            _currentPose = string.Empty;
            _poseHoldTimer = 0f;
        }

        public List<string> GetReachableSpells()
        {
            return spellTrie.GetReachableSpells();
        }
    }
}
