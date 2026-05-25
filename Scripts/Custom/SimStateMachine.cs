// ============================================================
// SimStateMachine.cs
// Scripts/Custom/SimStateMachine.cs
//
// State enum for SimPlayer AI. Logic lives in SimPlayer.cs.
// Phase 1: Idle, Travelling, Dead, OnCooldown.
// ============================================================

namespace Server.Custom
{
    public enum SimState
    {
        Idle,        // standing still, may speak
        Travelling,  // moving toward next location
        Dead,        // just died, waiting for cooldown
        OnCooldown   // invisible, not in world
    }
}
