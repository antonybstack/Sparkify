namespace Client
{
    public class User
    {
        public int Balance { get; private set; }

        public void Apply(Payment @event)
        {
            Balance += @event.Amount;
        }
    }

    public class Payment
    {
        public int Amount { get; set; }
    }
}
