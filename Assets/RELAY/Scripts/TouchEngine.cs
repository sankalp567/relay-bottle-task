namespace Relay
{
    /// <summary>The touch condition: a press of the button on the bench advances the step.</summary>
    public class TouchEngine : RelayEngine
    {
        public TouchButton button;

        public override string ConditionName => "Touch";

        protected override void OnTrialStarted() => button.gameObject.SetActive(true);
        protected override void OnTouchPressed() => AdvanceStep();
    }
}
