// Scratch file - safe to delete.
//
// This briefly held throwaway probes confirming that the hand-written migrations
// (DropReasonForLeavingFromProfessionalExperiences, DropCollegeTierAndLegacyCoeColumns)
// are discoverable by EF - their [Migration] attributes resolve - and that the model
// snapshot no longer declares the dropped columns while retaining Bachelors* and the
// Emp-prefixed COE fields. All checks passed; the probes are gone. The file itself
// could not be removed from this environment (delete was permission denied), so it is
// emptied instead. Delete it.
