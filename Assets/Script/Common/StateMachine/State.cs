namespace HHH.Common
{
    public class State
    {
        public StateMachine StateMachine { get; set; }

        public void ChangeState<T>() where T : State
        {
            StateMachine.ChangeState<T>();
        }

        public void StateEnter(State from, object data)
        {
            OnStateEnter(from, data);
        }

        public void StateTick()
        {
            OnStateUpdate();
        }

        public void StateFixedTick()
        {
            OnStateFixedUpdate();
        }

        public void StateExit(State to)
        {
            OnStateExit(to);
        }

        protected virtual void OnStateEnter(State from, object data) { }

        protected virtual void OnStateUpdate() { }

        protected virtual void OnStateFixedUpdate() { }

        protected virtual void OnStateExit(State to) { }

    }
}