using UnityEngine;

namespace Relay
{
    /// <summary>
    /// The slot for a logic engine: anything that decides when the instructions change. Subclass this and add
    /// it to RelayManager, and it appears as a condition in the session panel. Override the signals you need
    /// and call the step helpers or Presenter. Never read TaskTruth: it's the answer key used for scoring.
    /// </summary>
    public abstract class RelayEngine : MonoBehaviour
    {
        /// <summary>The name in the session panel and the data files.</summary>
        public abstract string ConditionName { get; }

        protected RelayPresenter Presenter { get; private set; }
        protected RelaySignals Signals { get; private set; }
        protected TaskStepData[] Steps => Presenter.source.Data.steps;

        /// <summary>The step the panel is showing, counted from 0.</summary>
        public int Current { get; private set; }
        public bool IsRunning { get; private set; }
        protected int LastStep => Steps.Length - 1;

        public void BeginTrial(RelayPresenter presenter, RelaySignals signals)
        {
            Presenter = presenter;
            Signals = signals;
            Current = 0;
            IsRunning = true;

            signals.LookStarted += OnLookStarted;
            signals.LookEnded += OnLookEnded;
            signals.Grabbed += OnGrabbed;
            signals.Released += OnReleased;
            signals.Arrived += OnArrived;
            signals.Departed += OnDeparted;
            signals.BackupPressed += OnBackupPressed;
            signals.TouchPressed += OnTouchPressed;

            presenter.TaskStarted();
            OnTrialStarted();
        }

        public void EndTrial()
        {
            if (!IsRunning) return;
            IsRunning = false;

            Signals.LookStarted -= OnLookStarted;
            Signals.LookEnded -= OnLookEnded;
            Signals.Grabbed -= OnGrabbed;
            Signals.Released -= OnReleased;
            Signals.Arrived -= OnArrived;
            Signals.Departed -= OnDeparted;
            Signals.BackupPressed -= OnBackupPressed;
            Signals.TouchPressed -= OnTouchPressed;
            OnTrialEnded();
        }

        /// <summary>Shows the next step's ghost. Commits nothing.</summary>
        protected void PreviewNext()
        {
            if (Current < LastStep) Presenter.Armed(Current + 1);
        }

        protected void CancelPreview() => Presenter.Disarmed();

        /// <summary>Moves to the next step. Advancing past the last step finishes the task.</summary>
        protected void AdvanceStep()
        {
            if (Current >= LastStep)
            {
                Presenter.Completed();
                return;
            }

            Current++;
            Presenter.Committed(Current);
        }

        protected void UndoStep()
        {
            if (Current <= 0) return;
            Current--;
            Presenter.RolledBack(Current);
        }

        protected virtual void OnTrialStarted() { }
        protected virtual void OnTrialEnded() { }
        protected virtual void OnLookStarted(string objectId) { }
        protected virtual void OnLookEnded(string objectId, double seconds) { }
        protected virtual void OnGrabbed(string objectId, string hand) { }
        protected virtual void OnReleased(string objectId, string hand) { }
        protected virtual void OnArrived(string mover, string target) { }
        protected virtual void OnDeparted(string mover, string target) { }
        protected virtual void OnTouchPressed() { }

        /// <summary>The standard backup: a tap advances, a double tap goes back.</summary>
        protected virtual void OnBackupPressed(int taps)
        {
            if (taps >= 2) UndoStep();
            else AdvanceStep();
        }
    }
}
