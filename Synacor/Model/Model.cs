using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Synacor.Annotations;

namespace Synacor {
    class Model {
        const int RLeft = 32768;
        const int RRight = 32775;
        const int ProgramEntry = 0;
        const int MaxMask = 0x7FFF;
        public const int MemorySize = 32768;
        readonly int[] memory;
        readonly int[] registers;
        int eip;
        int viewEIP;
        readonly Stack<int> stack;
        readonly Parser parser;
        readonly Instruction[] instructions;
        bool halted;
        bool pause;
        readonly StreamReader sr;
        readonly StreamWriter sw;
        readonly List<int> callStack;
        bool raiseEvents;
        readonly Dictionary<int, Breakpoint> breakpoints;

        public Model(StreamReader sr, StreamWriter sw) {
            memory = new int[MemorySize];
            registers = new int[8];
            parser = new Parser(memory, this);
            instructions = new Instruction[MemorySize];
            stack = new Stack<int>();
            EIP = ProgramEntry;
            viewEIP = 0;
            InstructionsView = new BindingList<InstructionView>();
            this.sr = sr;
            this.sw = sw;
            pause = false;
            callStack = new List<int>();
            breakpoints = new Dictionary<int, Breakpoint>();
            SetRaiseEvents(true);
        }

        public void Init(int[] data) {
            for (int i = 0; i < data.Length; i++) {
                WriteToMem(i, data[i]);
            }
            for (int i = 0; i < 8; i++) {
                WriteToRegistryCore(i, 0);
            }
            InitInstructions();
            ParseInstructionAtEIP();
        }

        void SetRaiseEvents(bool newValue) {
            raiseEvents = newValue;
            InstructionsView.RaiseListChangedEvents = newValue;
        }

        int ParseInstructionAtEIP() {
            return ParseInstructionAtPosition(EIP);
        }

        int ParseInstructionAtPosition(int p) {
            Instruction instruction = parser.ParseInstruction(p);
            for (int i = 0; i < instruction.Codes.Count; i++) {
                int t = instruction.Position + i;
                instructions[t] = instruction;
                int forDelete = BynarySearchPositionInInstructionView(t);
                if (forDelete >= 0 && (InstructionsView[forDelete].InstructionText != instruction.Text || InstructionsView[forDelete].Position != instruction.Position)) {
                    InstructionsView.RemoveAt(forDelete);
                }
            }
            int newViewIndex = BynarySearchPositionInInstructionView(instruction.Position);
            if (newViewIndex >= 0) {
                InstructionsView[newViewIndex].InstructionInfo = CalcInfo(instruction);
                return newViewIndex;
            }
            newViewIndex = ~newViewIndex;
            InstructionsView.Insert(newViewIndex, new InstructionView(instruction, CalcInfo(instruction)));
            return newViewIndex;
        }

        string CalcInfo(Instruction instruction) {
            switch (instruction.InstructionType) {
                case InstructionType.Halt:
                    return CalcInfoHalt();
                case InstructionType.Set:
                    return CalcInfoSet(instruction.Args[0], instruction.Args[1]);
                case InstructionType.Push:
                    return CalcInfoPush(instruction.Args[0]);
                case InstructionType.Pop:
                    return CalcInfoPop(instruction.Args[0]);
                case InstructionType.Eq:
                    return CalcInfoEq(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                case InstructionType.Gt:
                    return CalcInfoGt(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                case InstructionType.Jmp:
                    return CalcInfoJmp(instruction.Args[0]);
                case InstructionType.Jt:
                    return CalcInfoJt(instruction.Args[0], instruction.Args[1]);
                case InstructionType.Jf:
                    return CalcInfoJf(instruction.Args[0], instruction.Args[1]);
                case InstructionType.Add:
                    return CalcInfoAdd(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                case InstructionType.Mult:
                    return CalcInfoMult(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                case InstructionType.Mod:
                    return CalcInfoMod(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                case InstructionType.And:
                    return CalcInfoAnd(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                case InstructionType.Or:
                    return CalcInfoOr(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                case InstructionType.Not:
                    return CalcInfoNot(instruction.Args[0], instruction.Args[1]);
                case InstructionType.Rmem:
                    return CalcInfoRmem(instruction.Args[0], instruction.Args[1]);
                case InstructionType.Wmem:
                    return CalcInfoWmem(instruction.Args[0], instruction.Args[1]);
                case InstructionType.Call:
                    return CalcInfoCall(instruction.Args[0]);
                case InstructionType.Ret:
                    return CalcInfoRet();
                case InstructionType.Output:
                    return CalcInfoOut(instruction.Args[0]);
                case InstructionType.Input:
                    return CalcInfoIn(instruction.Args[0]);
                case InstructionType.Noop:
                    return CalcInfoNoop();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal string GetRegisterOrNumber(int value) {
            return value >= RLeft ? GetRegisterName(value) : GetNumber(value);
        }

        internal string GetNumber(int value) {
            if (value < 0 || value >= RLeft)
                throw new Exception();
            return value.ToString("X4");
        }

        internal string GetRegisterName(int value) {
            if (value < RLeft || value > RRight)
                throw new Exception();
            return $"R{value - RLeft}";
        }

        string CalcInfoNoop() {
            return string.Empty;
        }

        string CalcInfoIn(int a) {
            return $"{GetRegisterOrNumber(a)} = ReadChar();";
        }

        string CalcInfoOut(int a) {
            return $"WriteChar('{((char) ReadFrom(a)).ToString()}');";
        }

        string CalcInfoRet() {
            return $"return;";
        }

        string CalcInfoCall(int a) {
            a = ReadFrom(a);
            return $"{a.ToString("X4")}();";
        }

        string CalcInfoWmem(int a, int b) {
            a = ReadFrom(a);
            b = ReadFrom(b);
            return $"[{a.ToString("X4")}] = {b.ToString()};";
        }

        string CalcInfoRmem(int a, int b) {
            b = ReadFrom(b);
            int ans = ReadMemory(b);
            return $"{GetRegisterOrNumber(a)} = [{b.ToString("X4")}]; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoNot(int a, int b) {
            b = ReadFrom(b);
            int ans = (~ReadFrom(b)) & MaxMask;
            return $"{GetRegisterOrNumber(a)} = ~{b.ToString()}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoOr(int a, int b, int c) {
            b = ReadFrom(b);
            c = ReadFrom(c);
            int ans = (b | c) % MemorySize;
            return $"{GetRegisterOrNumber(a)} = {b.ToString()} | {c.ToString()}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoAnd(int a, int b, int c) {
            b = ReadFrom(b);
            c = ReadFrom(c);
            int ans = (b & c) % MemorySize;
            return $"{GetRegisterOrNumber(a)} = {b.ToString()} & {c.ToString()}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoMod(int a, int b, int c) {
            b = ReadFrom(b);
            c = ReadFrom(c);
            int ans = (b % c) % MemorySize;
            return $"{GetRegisterOrNumber(a)} = {b.ToString()} % {c.ToString()}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoMult(int a, int b, int c) {
            b = ReadFrom(b);
            c = ReadFrom(c);
            int ans = (b * c) % MemorySize;
            return $"{GetRegisterOrNumber(a)} = {b.ToString()} * {c.ToString()}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoAdd(int a, int b, int c) {
            b = ReadFrom(b);
            c = ReadFrom(c);
            int ans = (b + c) % MemorySize;
            return $"{GetRegisterOrNumber(a)} = {b.ToString()} + {c.ToString()}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoJf(int a, int b) {
            a = ReadFrom(a);
            b = ReadFrom(b);
            bool ans = a == 0;
            return $"if ({a.ToString()} == 0) goto {b.ToString("X4")}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoJt(int a, int b) {
            a = ReadFrom(a);
            b = ReadFrom(b);
            bool ans = a != 0;
            return $"if ({a.ToString()} != 0) goto {b.ToString("X4")}; {{{ans.ToString().ToUpper()}}}";
        }

        string CalcInfoJmp(int a) {
            return $"goto {ReadFrom(a).ToString("X4")}";
        }

        string CalcInfoGt(int a, int b, int c) {
            b = ReadFrom(b);
            c = ReadFrom(c);
            int ans = b > c ? 1 : 0;
            return $"{GetRegisterOrNumber(a)} = {b.ToString()} > {c.ToString()} ? 1 : 0; {{{ans.ToString()}}}";
        }

        string CalcInfoEq(int a, int b, int c) {
            b = ReadFrom(b);
            c = ReadFrom(c);
            int ans = b == c ? 1 : 0;
            return $"{GetRegisterOrNumber(a)} = {b.ToString()} == {c.ToString()} ? 1 : 0; {{{ans.ToString()}}}";
        }

        string CalcInfoPop(int a) {
            return $"{GetRegisterOrNumber(a)} = Pop();";
        }

        string CalcInfoPush(int a) {
            return $"Push({ReadFrom(a).ToString()});";
        }

        string CalcInfoSet(int a, int b) {
            return $"{GetRegisterName(a)} = {ReadFrom(b).ToString()};";
        }

        string CalcInfoHalt() {
            return "Halt();";
        }

        int BynarySearchPositionInInstructionView(int p) {
            int left = 0;
            int right = InstructionsView.Count - 1;
            while (left <= right) {
                int middle = (right - left) / 2 + left;
                int actualP = InstructionsView[middle].Position;
                if (actualP == p) {
                    return middle;
                }
                if (actualP > p) {
                    right = middle - 1;
                }
                else {
                    left = middle + 1;
                }
            }
            return ~left;
        }

        void InitInstructions() {
            for (int index = 0; index < memory.Length; index++) {
                int m = memory[index];
                instructions[index] = new Instruction(InstructionType.Unknown, m.ToString("X4"), new int[0], new[] {m}, index);
                InstructionsView.Add(new InstructionView(instructions[index], String.Empty));
            }
        }

        bool IsRegister(int p) {
            return p >= RLeft && p <= RRight;
        }

        int ReadFrom(int p) {
            return IsRegister(p) ? ReadRegistry(p) : p;
        }

        int ReadRegistry(int p) {
            Breakpoint bp;
            if(breakpoints.TryGetValue(p, out bp) && bp.Read)
                pause = true;
            return registers[p - RLeft];
        }

        int ReadMemory(int p) {
            Breakpoint bp;
            if(breakpoints.TryGetValue(p, out bp) && bp.Read)
                pause = true;
            return memory[p];
        }

        void WriteTo(int p, int value) {
            if (IsRegister(p))
                WriteToRegistry(p, value);
            else
                WriteToMem(p, value);
        }

        void WriteToMem(int p, int value) {
            Breakpoint bp;
            if (breakpoints.TryGetValue(p, out bp) && bp.Write)
                pause = true;
            memory[p] = value;
            RaiseMemoryChanged(p, value);
        }

        void WriteToRegistry(int p, int value) {
            Breakpoint bp;
            if(breakpoints.TryGetValue(p, out bp) && bp.Write)
                pause = true;
            WriteToRegistryCore(p - RLeft, value);
        }

        void WriteToRegistryCore(int p, int value) {
            registers[p] = value;
            RaiseRegistryChanged(p, value);
        }

        int PopFromStack() {
            int result = stack.Pop();
            RaiseStackPoped();
            return result;
        }

        void RaiseStackPoped() {
            if (!raiseEvents)
                return;
            onStackPoped?.Invoke(this, EventArgs.Empty);
        }

        EventHandler<EventArgs> onStackPoped;
        public event EventHandler<EventArgs> OnStackPoped { add { onStackPoped += value; } remove { onStackPoped -= value; } }

        void PushToStack(int value) {
            stack.Push(value);
            RaiseStackPushed(value);
        }

        void RaiseStackPushed(int value) {
            if (!raiseEvents)
                return;
            onStackPushed?.Invoke(this, new StackPushedEventArgs(value));
        }

        EventHandler<StackPushedEventArgs> onStackPushed;
        public event EventHandler<StackPushedEventArgs> OnStackPushed { add { onStackPushed += value; } remove { onStackPushed -= value; } }


        void RaiseRegistryChanged(int position, int newValue) {
            if (!raiseEvents)
                return;
            onRegistryChanged?.Invoke(this, new MemoryChangedEventArgs(position, newValue));
        }

        EventHandler<MemoryChangedEventArgs> onRegistryChanged;
        public event EventHandler<MemoryChangedEventArgs> OnRegistryChanged { add { onRegistryChanged += value; } remove { onRegistryChanged -= value; } }

        void RaiseMemoryChanged(int position, int newValue) {
            if (!raiseEvents)
                return;
            onMemoryChanged?.Invoke(this, new MemoryChangedEventArgs(position, newValue));
        }

        EventHandler<MemoryChangedEventArgs> onMemoryChanged;
        public event EventHandler<MemoryChangedEventArgs> OnMemoryChanged { add { onMemoryChanged += value; } remove { onMemoryChanged -= value; } }

        internal int ViewEIP {
            get { return EIP; }
            set {
                int oldValue = viewEIP;
                viewEIP = value;
                RaiseOnViewEIPChanged(oldValue, value);
            }
        }

        void RaiseOnViewEIPChanged(int oldValue, int newValue) {
            if (!raiseEvents)
                return;
            onViewEipChanged?.Invoke(this, new ChangedEIPEventArgs(newValue, oldValue));
        }

        EventHandler<ChangedEIPEventArgs> onViewEipChanged;
        public event EventHandler<ChangedEIPEventArgs> OnViewEIPChanged { add { onViewEipChanged += value; } remove { onViewEipChanged -= value; } }

        public BindingList<InstructionView> InstructionsView { get; private set; }

        public int EIP {
            get { return eip; }
            set {
                if (eip >= MemorySize)
                    throw new Exception();
                eip = value;
            }
        }

        internal void UpdateView() {
            SetRaiseEvents(true);
            InstructionsView.ResetBindings();
            int newViewIndex = BynarySearchPositionInInstructionView(instructions[EIP].Position);
            RaiseOnViewEIPChanged(0, newViewIndex);
            for (int i = 0; i < MemorySize; i++) {
                RaiseMemoryChanged(i, memory[i]);
            }
            for (int i = 0; i < 8; i++) {
                RaiseRegistryChanged(i, registers[i]);
            }
            RaiseStackReset(stack.ToArray(), callStack.ToArray());
            SetRaiseEvents(false);
        }

        void RaiseStackReset(int[] stackA, int[] callStackA) {
            if (!raiseEvents)
                return;
            onStackReset?.Invoke(this, new StackResetEventArgs(stackA, callStackA));
        }

        EventHandler<StackResetEventArgs> onStackReset;
        public event EventHandler<StackResetEventArgs> OnStackReset { add { onStackReset += value; } remove { onStackReset -= value; } }

        public bool StepInto() {
            SetRaiseEvents(true);
            bool result = StepIntoCore();
            SetRaiseEvents(false);
            return result;
        }

        public bool StepIntoCore() {
            if (halted)
                return false;
            Instruction currentInstruction = instructions[EIP];
            EIP += currentInstruction.Codes.Count;
            ExecuteInstruction(currentInstruction);
            ViewEIP = ParseInstructionAtEIP();
            for (int i = 0; i < instructions[EIP].Codes.Count; i++) {
                int p = instructions[EIP].Position + i;
                Breakpoint bp;
                if (breakpoints.TryGetValue(p, out bp) && bp.Execute) {
                    pause = true;
                    break;
                }
            }
            if (halted)
                return false;
            if (pause) {
                pause = false;
                return false;
            }
            return true;
        }

        int stepOverCounter = 0;

        public void StepOver() {
            if (instructions[EIP].InstructionType != InstructionType.Call) {
                StepInto();
                return;
            }

            stepOverCounter = 0;
            do {
                StepIntoCore();
            } while (stepOverCounter > 0);
            UpdateView();
        }

        void ExecuteInstruction(Instruction instruction) {
            switch (instruction.InstructionType) {
                case InstructionType.Halt:
                    ExecuteHalt();
                    break;
                case InstructionType.Set:
                    ExecuteSet(instruction.Args[0], instruction.Args[1]);
                    break;
                case InstructionType.Push:
                    ExecutePush(instruction.Args[0]);
                    break;
                case InstructionType.Pop:
                    ExecutePop(instruction.Args[0]);
                    break;
                case InstructionType.Eq:
                    ExecuteEq(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case InstructionType.Gt:
                    ExecuteGt(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case InstructionType.Jmp:
                    ExecuteJmp(instruction.Args[0]);
                    break;
                case InstructionType.Jt:
                    ExecuteJt(instruction.Args[0], instruction.Args[1]);
                    break;
                case InstructionType.Jf:
                    ExecuteJf(instruction.Args[0], instruction.Args[1]);
                    break;
                case InstructionType.Add:
                    ExecuteAdd(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case InstructionType.Mult:
                    ExecuteMult(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case InstructionType.Mod:
                    ExecuteMod(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case InstructionType.And:
                    ExecuteAnd(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case InstructionType.Or:
                    ExecuteOr(instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case InstructionType.Not:
                    ExecuteNot(instruction.Args[0], instruction.Args[1]);
                    break;
                case InstructionType.Rmem:
                    ExecuteRmem(instruction.Args[0], instruction.Args[1]);
                    break;
                case InstructionType.Wmem:
                    ExecuteWmem(instruction.Args[0], instruction.Args[1]);
                    break;
                case InstructionType.Call:
                    ExecuteCall(instruction.Args[0]);
                    break;
                case InstructionType.Ret:
                    ExecuteRet();
                    break;
                case InstructionType.Output:
                    ExecuteOut(instruction.Args[0]);
                    break;
                case InstructionType.Input:
                    ExecuteIn(instruction.Args[0]);
                    break;
                case InstructionType.Noop:
                    ExecuteNoop();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void ExecuteNoop() {

        }

        void ExecuteIn(int a) {
            int c = sr.Read();
            if (c == 0xD)
                c = sr.Read();
            WriteTo(a, c);
        }

        void ExecuteOut(int a) {
            sw.Write(new[] {(char) ReadFrom(a)});
        }

        void ExecuteRet() {
            stepOverCounter--;
            EIP = PopFromStack();
            callStack.RemoveAt(callStack.Count - 1);
            RaiseMethodReturn();
        }

        void RaiseMethodReturn() {
            if (!raiseEvents)
                return;
            onMethodReturn?.Invoke(this, EventArgs.Empty);
        }

        EventHandler<EventArgs> onMethodReturn;
        public event EventHandler<EventArgs> OnMethodReturn { add { onMethodReturn += value; } remove { onMethodReturn -= value; } }

        void ExecuteCall(int a) {
            stepOverCounter++;
            PushToStack(EIP);
            callStack.Add(EIP);
            RaiseMethodCalled(EIP);
            EIP = ReadFrom(a);
        }

        void RaiseMethodCalled(int retIP) {
            if (!raiseEvents)
                return;
            onMethodCalled?.Invoke(this, new MethodCalledEventArgs(retIP));
        }

        EventHandler<MethodCalledEventArgs> onMethodCalled;
        public event EventHandler<MethodCalledEventArgs> OnMethodCalled { add { onMethodCalled += value; } remove { onMethodCalled -= value; } }

        void ExecuteWmem(int a, int b) {
            WriteTo(ReadFrom(a), ReadFrom(b));
        }

        void ExecuteRmem(int a, int b) {
            WriteTo(a, ReadMemory(ReadFrom(b)));
        }

        void ExecuteNot(int a, int b) {
            WriteTo(a, (~ReadFrom(b)) & MaxMask);
        }

        void ExecuteOr(int a, int b, int c) {
            WriteTo(a, ReadFrom(b) | ReadFrom(c));
        }

        void ExecuteAnd(int a, int b, int c) {
            WriteTo(a, ReadFrom(b) & ReadFrom(c));
        }

        void ExecuteMod(int a, int b, int c) {
            WriteTo(a, (ReadFrom(b) % ReadFrom(c)) % MemorySize);
        }

        void ExecuteMult(int a, int b, int c) {
            WriteTo(a, (ReadFrom(b) * ReadFrom(c)) % MemorySize);
        }

        void ExecuteAdd(int a, int b, int c) {
            WriteTo(a, (ReadFrom(b) + ReadFrom(c)) % MemorySize);
        }

        void ExecuteJf(int a, int b) {
            if (ReadFrom(a) == 0)
                EIP = ReadFrom(b);
        }

        void ExecuteJt(int a, int b) {
            if (ReadFrom(a) != 0)
                EIP = ReadFrom(b);
        }

        void ExecuteJmp(int a) {
            EIP = ReadFrom(a);
        }

        void ExecuteGt(int a, int b, int c) {
            WriteTo(a, ReadFrom(b) > ReadFrom(c) ? 1 : 0);
        }

        void ExecuteEq(int a, int b, int c) {
            WriteTo(a, ReadFrom(b) == ReadFrom(c) ? 1 : 0);
        }

        void ExecutePop(int a) {
            WriteTo(a, PopFromStack());
        }

        void ExecutePush(int a) {
            PushToStack(ReadFrom(a));
        }

        void ExecuteSet(int a, int b) {
            WriteToRegistry(a, ReadFrom(b));
        }

        void ExecuteHalt() {
            halted = true;
            RaiseHalt();
        }

        void RaiseHalt() {
            onHalt?.Invoke(this, EventArgs.Empty);
        }

        EventHandler onHalt;
        public event EventHandler OnHalt { add { onHalt += value; } remove { onHalt -= value; } }

        public void Pause() {
            pause = true;
            UpdateView();
        }

        public void Stop() {
            halted = true;
            UpdateView();
        }

        public void Continue() {
            SetRaiseEvents(false);
            while (StepIntoCore()) {
            }
            SetRaiseEvents(true);
        }

        public void ToggleBreakpoint(string ipString, bool execute, bool write, bool read) {
            int ip = ipString.StartsWith("0x") ? Convert.ToInt32(ipString.Substring(2), 16) : Convert.ToInt32(ipString);
            if (breakpoints.ContainsKey(ip)) {
                breakpoints.Remove(ip);
                RaiseBreakpointToogle(ip, false);
            }
            else {
                Breakpoint bp = new Breakpoint(ip, execute, write, read);
                breakpoints.Add(ip, bp);
                RaiseBreakpointToogle(ip, true);
            }
        }

        void RaiseBreakpointToogle(int position, bool enable) {
            onBreakpointToggle?.Invoke(this, new BreakpointToggleEventArgs(position, enable));
        }

        EventHandler<BreakpointToggleEventArgs> onBreakpointToggle;
        public event EventHandler<BreakpointToggleEventArgs> OnBreakpointToggle { add { onBreakpointToggle += value; } remove { onBreakpointToggle -= value; } }

        public void SaveState(string fileName) {
            using (var f = File.Create(fileName)) {
                using (BinaryWriter bw = new BinaryWriter(f)) {
                    foreach (int m in memory) {
                        bw.Write(m);
                    }
                    foreach (int r in registers) {
                        bw.Write(r);
                    }
                    bw.Write(EIP);
                    bw.Write(breakpoints.Count);
                    foreach (KeyValuePair<int, Breakpoint> breakpoint in breakpoints) {
                        bw.Write(breakpoint.Value.Position);
                        bw.Write(breakpoint.Value.Execute);
                        bw.Write(breakpoint.Value.Write);
                        bw.Write(breakpoint.Value.Read);
                    }
                    bw.Write(stack.Count);
                    foreach (int i in stack.ToArray()) {
                        bw.Write(i);
                    }
                    bw.Write(callStack.Count);
                    foreach (int i in callStack) {
                        bw.Write(i);
                    }
                }
            }
        }

        public void LoadState(string fileName) {
            using (var f = File.OpenRead(fileName)) {
                using (BinaryReader br = new BinaryReader(f)) {
                    for (int i = 0; i < MemorySize; i++) {
                        memory[i] = br.ReadInt32();
                    }
                    for (int i = 0; i < 8; i++) {
                        registers[i] = br.ReadInt32();
                    }
                    EIP = br.ReadInt32();
                    breakpoints.Clear();
                    int l = br.ReadInt32();
                    for (int i = 0; i < l; i++) {
                        Breakpoint bp = new Breakpoint(br.ReadInt32(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean());
                        breakpoints.Add(bp.Position, bp);
                    }
                    int[] t = new int[br.ReadInt32()];
                    for (int i = 0; i < t.Length; i++) {
                        t[i] = br.ReadInt32();
                    }
                    stack.Clear();
                    for (int i = t.Length - 1; i >= 0; i--) {
                        stack.Push(t[i]);
                    }
                    callStack.Clear();
                    l = br.ReadInt32();
                    for (int i = 0; i < l; i++) {
                        callStack.Add(br.ReadInt32());
                    }
                }
            }
            ParseInstructionAtEIP();
            UpdateView();
        }
    }

    class BreakpointToggleEventArgs : EventArgs {
        public BreakpointToggleEventArgs(int position, bool enable) {
            Enable = enable;
            Position = position;
        }
        public bool Enable { get; private set; }
        public int Position { get; private set; }
    }

    class Breakpoint {
        public Breakpoint(int position, bool execute, bool write, bool read) {
            Position = position;
            Write = write;
            Execute = execute;
            Read = read;
        }

        public int Position { get; private set; }
        public bool Write { get; private set; }
        public bool Execute { get; private set; }
        public bool Read { get; private set; }
    }

    class InstructionView:INotifyPropertyChanged {
        string instructionInfo;

        public InstructionView(Instruction instruction, string instructionInfo) {
            Position = instruction.Position;
            PositionText = Position.ToString("X4");
            InstructionText = instruction.Text;
            InstructionInfo = instructionInfo;
        }

        public int Position { get; private set; }
        public string PositionText { get; private set; }
        public string InstructionText { get; private set; }

        public string InstructionInfo {
            get { return instructionInfo; }
            set {
                instructionInfo = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    class ChangedEIPEventArgs : EventArgs {
        public int NewValue { get; private set; }
        public int OldValue { get; private set; }

        public ChangedEIPEventArgs(int newValue, int oldValue) {
            NewValue = newValue;
            OldValue = oldValue;
        }
    }

    class StackResetEventArgs : EventArgs {
        public int[] Stack { get; private set; }
        public int[] CallStack { get; private set; }

        public StackResetEventArgs(int[] stack, int[] callStack) {
            Stack = stack;
            CallStack = callStack;
        }
    }

    class MemoryChangedEventArgs : EventArgs {
        public int Position { get; private set; }
        public int NewValue { get; private set; }

        public MemoryChangedEventArgs(int position, int newValue) {
            Position = position;
            NewValue = newValue;
        }
    }

    class StackPushedEventArgs : EventArgs {
        public int Value { get; private set; }

        public StackPushedEventArgs(int value) {
            Value = value;
        }
    }

    class MethodCalledEventArgs : EventArgs {
        public MethodCalledEventArgs(int retIp) {
            RetIP = retIp;
        }

        public int RetIP { get; private set; }
        
    }

    enum InstructionType {
        Halt = 0,
        Set = 1,
        Push = 2,
        Pop = 3,
        Eq = 4,
        Gt = 5,
        Jmp = 6,
        Jt = 7,
        Jf = 8,
        Add = 9,
        Mult = 10,
        Mod = 11,
        And = 12,
        Or = 13,
        Not = 14,
        Rmem = 15,
        Wmem = 16,
        Call = 17,
        Ret = 18,
        Output = 19,
        Input = 20,
        Noop = 21,
        Unknown = -1
    }

    class Instruction {
        readonly string stringCodes;

        public Instruction(InstructionType instructionType, string text, int[] args, int[] codes, int pos) {
            Text = text;
            Args = Array.AsReadOnly(args);
            Codes = Array.AsReadOnly(codes);
            InstructionType = instructionType;
            StringBuilder sb = new StringBuilder();
            foreach (int code in codes) {
                byte[] bytes = BitConverter.GetBytes((ushort) code);
                foreach (byte b in bytes) {
                    sb.Append(b.ToString("X2"));
                }
            }
            stringCodes = sb.ToString();
            Position = pos;
        }

        public int Position { get; private set; }
        public InstructionType InstructionType { get; private set; }
        public string Text { get; private set; }
        public ReadOnlyCollection<int> Args { get; private set; }
        public ReadOnlyCollection<int> Codes { get; private set; }

        public override string ToString() {
            return stringCodes;
        }
    }

    class Parser {
        int index;
        int[] data;
        Model model;

        public Parser(int[] data, Model model) {
            this.data = data;
            index = 0;
            this.model = model;
        }

        int Next() {
            if (index >= data.Length)
                return -1;
            return data[index++];
        }

        Instruction ParseNoop() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[0];
            int[] codes = new int[1];
            sb.Append("noop ");
            codes[0] = 21;
            return new Instruction(InstructionType.Noop, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseIn() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[1];
            int[] codes = new int[2];
            sb.Append("in ");
            codes[0] = 20;
            codes[1] = Next();
            args[0] = codes[1];
            sb.Append(GetRegisterOrNumber(args[0]));
            return new Instruction(InstructionType.Input, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseOut() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[1];
            int[] codes = new int[2];
            sb.Append("out ");
            codes[0] = 19;
            codes[1] = Next();
            args[0] = codes[1];
            sb.Append(GetRegisterOrNumber(args[0]));
            return new Instruction(InstructionType.Output, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseRet() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[0];
            int[] codes = new int[1];
            sb.Append("ret ");
            codes[0] = 18;
            return new Instruction(InstructionType.Ret, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseCall() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[1];
            int[] codes = new int[2];
            sb.Append("call ");
            codes[0] = 17;
            codes[1] = Next();
            args[0] = codes[1];
            sb.Append(GetRegisterOrNumber(args[0]));
            return new Instruction(InstructionType.Call, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseWmem() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[2];
            int[] codes = new int[3];
            sb.Append("wmem ");
            codes[0] = 16;
            codes[1] = Next();
            codes[2] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            return new Instruction(InstructionType.Wmem, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseRmem() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[2];
            int[] codes = new int[3];
            sb.Append("rmem ");
            codes[0] = 15;
            codes[1] = Next();
            codes[2] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            return new Instruction(InstructionType.Rmem, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseNot() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[2];
            int[] codes = new int[3];
            sb.Append("not ");
            codes[0] = 14;
            codes[1] = Next();
            codes[2] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            return new Instruction(InstructionType.Not, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseOr() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[3];
            int[] codes = new int[4];
            sb.Append("or ");
            codes[0] = 13;
            codes[1] = Next();
            codes[2] = Next();
            codes[3] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            args[2] = codes[3];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[2]));
            return new Instruction(InstructionType.Or, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseAnd() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[3];
            int[] codes = new int[4];
            sb.Append("and ");
            codes[0] = 12;
            codes[1] = Next();
            codes[2] = Next();
            codes[3] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            args[2] = codes[3];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[2]));
            return new Instruction(InstructionType.And, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseMod() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[3];
            int[] codes = new int[4];
            sb.Append("mod ");
            codes[0] = 11;
            codes[1] = Next();
            codes[2] = Next();
            codes[3] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            args[2] = codes[3];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[2]));
            return new Instruction(InstructionType.Mod, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseMult() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[3];
            int[] codes = new int[4];
            sb.Append("mult ");
            codes[0] = 10;
            codes[1] = Next();
            codes[2] = Next();
            codes[3] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            args[2] = codes[3];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[2]));
            return new Instruction(InstructionType.Mult, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseAdd() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[3];
            int[] codes = new int[4];
            sb.Append("add ");
            codes[0] = 9;
            codes[1] = Next();
            codes[2] = Next();
            codes[3] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            args[2] = codes[3];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[2]));
            return new Instruction(InstructionType.Add, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseJf() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[2];
            int[] codes = new int[3];
            sb.Append("jf ");
            codes[0] = 8;
            codes[1] = Next();
            codes[2] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            return new Instruction(InstructionType.Jf, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseJt() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[2];
            int[] codes = new int[3];
            sb.Append("jt ");
            codes[0] = 7;
            codes[1] = Next();
            codes[2] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            return new Instruction(InstructionType.Jt, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseJmp() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[1];
            int[] codes = new int[2];
            sb.Append("jmp ");
            codes[0] = 6;
            codes[1] = Next();
            args[0] = codes[1];
            sb.Append(GetRegisterOrNumber(args[0]));
            return new Instruction(InstructionType.Jmp, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseGt() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[3];
            int[] codes = new int[4];
            sb.Append("gt ");
            codes[0] = 5;
            codes[1] = Next();
            codes[2] = Next();
            codes[3] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            args[2] = codes[3];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[2]));
            return new Instruction(InstructionType.Gt, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseEq() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[3];
            int[] codes = new int[4];
            sb.Append("eq ");
            codes[0] = 4;
            codes[1] = Next();
            codes[2] = Next();
            codes[3] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            args[2] = codes[3];
            sb.Append(GetRegisterOrNumber(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[2]));
            return new Instruction(InstructionType.Eq, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParsePop() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[1];
            int[] codes = new int[2];
            sb.Append("pop ");
            codes[0] = 3;
            codes[1] = Next();
            args[0] = codes[1];
            sb.Append(GetRegisterOrNumber(args[0]));
            return new Instruction(InstructionType.Pop, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParsePush() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[1];
            int[] codes = new int[2];
            sb.Append("push ");
            codes[0] = 2;
            codes[1] = Next();
            args[0] = codes[1];
            sb.Append(GetRegisterOrNumber(args[0]));
            return new Instruction(InstructionType.Push, sb.ToString(), args, codes, index - codes.Length);
        }

        string GetRegisterOrNumber(int value) {
            return model.GetRegisterOrNumber(value);
        }

        string GetNumber(int value) {
            return model.GetNumber(value);
        }

        string GetRegisterName(int value) {
            return model.GetRegisterName(value);
        }

        Instruction ParseSet() {
            StringBuilder sb = new StringBuilder();
            int[] args = new int[2];
            int[] codes = new int[3];
            sb.Append("set ");
            codes[0] = 1;
            codes[1] = Next();
            codes[2] = Next();
            args[0] = codes[1];
            args[1] = codes[2];
            sb.Append(GetRegisterName(args[0]));
            sb.Append(' ');
            sb.Append(GetRegisterOrNumber(args[1]));
            return new Instruction(InstructionType.Set, sb.ToString(), args, codes, index - codes.Length);
        }

        Instruction ParseHalt() {
            return new Instruction(InstructionType.Halt, "halt", new int[0], new[] {0}, index - 1);
        }

        public Instruction ParseInstruction(int position) {
            index = position;
            int op = Next();
            InstructionType instructionType = (InstructionType) op;
            Instruction result;
            switch (instructionType) {
                case InstructionType.Halt:
                    result = ParseHalt();
                    break;
                case InstructionType.Set:
                    result = ParseSet();
                    break;
                case InstructionType.Push:
                    result = ParsePush();
                    break;
                case InstructionType.Pop:
                    result = ParsePop();
                    break;
                case InstructionType.Eq:
                    result = ParseEq();
                    break;
                case InstructionType.Gt:
                    result = ParseGt();
                    break;
                case InstructionType.Jmp:
                    result = ParseJmp();
                    break;
                case InstructionType.Jt:
                    result = ParseJt();
                    break;
                case InstructionType.Jf:
                    result = ParseJf();
                    break;
                case InstructionType.Add:
                    result = ParseAdd();
                    break;
                case InstructionType.Mult:
                    result = ParseMult();
                    break;
                case InstructionType.Mod:
                    result = ParseMod();
                    break;
                case InstructionType.And:
                    result = ParseAnd();
                    break;
                case InstructionType.Or:
                    result = ParseOr();
                    break;
                case InstructionType.Not:
                    result = ParseNot();
                    break;
                case InstructionType.Rmem:
                    result = ParseRmem();
                    break;
                case InstructionType.Wmem:
                    result = ParseWmem();
                    break;
                case InstructionType.Call:
                    result = ParseCall();
                    break;
                case InstructionType.Ret:
                    result = ParseRet();
                    break;
                case InstructionType.Output:
                    result = ParseOut();
                    break;
                case InstructionType.Input:
                    result = ParseIn();
                    break;
                case InstructionType.Noop:
                    result = ParseNoop();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return result;
        }
    }
}