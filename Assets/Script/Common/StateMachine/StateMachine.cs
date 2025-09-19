using System.Collections.Generic;
using UnityEngine;

namespace HHH.Common
{
    public class StateMachine
    {
        public State CurrentState { get; private set; }
        public List<State> States { get; set; }

        public void InitStates(List<State> states)
        {
            States = states;
            foreach (var state in states) state.StateMachine = this;
        }

        public void ChangeState<T>(object data = null) where T : State
        {
            if (States != null && States.Count > 0)
            {
                foreach (var state in States)
                {
                    if(state is T)
                    {
                        ChangeState(state,data);
                        return;
                    }
                }
                Debug.LogWarning($"Cannot find {typeof(T)}");
            }
        }

        void ChangeState(State state, object data = null)
        {
            if(state == CurrentState) return;

            var oldState = CurrentState;
            CurrentState = state;
            oldState?.StateExit(state);
            state?.StateEnter(oldState, data);
        }

        public void Tick() => CurrentState?.StateTick();

        public void FixeqTick() => CurrentState?.StateFixedTick();
    }
}