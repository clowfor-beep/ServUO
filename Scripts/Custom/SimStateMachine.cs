// ============================================================
// SimStateMachine.cs
// Scripts/Custom/SimStateMachine.cs
//
// State enum for SimPlayer AI. Logic lives in SimPlayer.cs.
// Phase 1: Idle, Travelling, Dead, OnCooldown.
// Phase 2: Banking added.
// ============================================================

namespace Server.Custom
{
    public enum SimState
    {
        Idle,        // standing still, may speak
        Travelling,  // moving toward next location
        Banking,     // at Britain bank — say "bank", loiter, bank chat
        Dead,        // just died, waiting for cooldown
        OnCooldown   // invisible, not in world
    }
}
