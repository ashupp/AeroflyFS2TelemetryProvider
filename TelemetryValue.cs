namespace SimFeedback.telemetry
{
    public class AeroflyFS2TelemetryValue : AbstractTelemetryValue
    {
        public AeroflyFS2TelemetryValue(string name, object value) : base()
        {
            Name = name;
            Value = value;
        }

        public override object Value { get; set; }

        public override string ToString()
        {
            return string.Format("{0} {1}", this.Value, this.Unit);
        }
    }
}