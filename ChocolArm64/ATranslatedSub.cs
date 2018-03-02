using ChocolArm64.Memory;
using ChocolArm64.State;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;

namespace ChocolArm64
{
    class ATranslatedSub
    {
        private delegate long AA64Subroutine(AThreadState Register, AMemory Memory);

        private AA64Subroutine ExecDelegate;

        private bool HasDelegate;

        public static int StateArgIdx  { get; private set; }
        public static int MemoryArgIdx { get; private set; }

        public static Type[] FixedArgTypes { get; private set; }

        public DynamicMethod Method { get; private set; }

        public ReadOnlyCollection<ARegister> Params { get; private set; }

        private HashSet<long> Callees;

        public bool NeedsReJit { get; private set; }

        public ATranslatedSub(DynamicMethod Method, List<ARegister> Params, HashSet<long> Callees)
        {
            if (Method == null)
            {
                throw new ArgumentNullException(nameof(Method));
            }

            if (Params == null)
            {
                throw new ArgumentNullException(nameof(Params));
            }

            if (Callees == null)
            {
                throw new ArgumentNullException(nameof(Callees));
            }

            this.Method  = Method;
            this.Params  = Params.AsReadOnly();
            this.Callees = Callees;
        }

        static ATranslatedSub()
        {
            MethodInfo MthdInfo = typeof(AA64Subroutine).GetMethod("Invoke");

            ParameterInfo[] Params = MthdInfo.GetParameters();

            FixedArgTypes = new Type[Params.Length];

            for (int Index = 0; Index < Params.Length; Index++)
            {
                Type ParamType = Params[Index].ParameterType;

                FixedArgTypes[Index] = ParamType;

                if (ParamType == typeof(AThreadState))
                {
                    StateArgIdx = Index;
                }
                else if (ParamType == typeof(AMemory))
                {
                    MemoryArgIdx = Index;
                }
            }
        }

        public long Execute(AThreadState ThreadState, AMemory Memory)
        {
            if (!HasDelegate)
            {
                string Name = $"{Method.Name}_Dispatch";

                DynamicMethod Mthd = new DynamicMethod(Name, typeof(long), FixedArgTypes);

                ILGenerator Generator = Mthd.GetILGenerator();

                Generator.EmitLdargSeq(FixedArgTypes.Length);

                foreach (ARegister Reg in Params)
                {
                    Generator.EmitLdarg(StateArgIdx);

                    Generator.Emit(OpCodes.Ldfld, Reg.GetField());
                }

                Generator.Emit(OpCodes.Call, Method);
                Generator.Emit(OpCodes.Ret);

                ExecDelegate = (AA64Subroutine)Mthd.CreateDelegate(typeof(AA64Subroutine));

                HasDelegate = true;
            }

            return ExecDelegate(ThreadState, Memory);
        }

        public bool HasCallee(long Position) => Callees.Contains(Position);

        public void MarkForReJit() => NeedsReJit = true;
    }
}