using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Be.Windows.Forms;
using Synacor.Utils;

namespace Synacor {
    public sealed partial class Form1 : Form {
        Color eipColor = Color.Red;
        Model model;
        bool halted;
        bool running;
        readonly ConsoleHelper consoleHelper;
        public Form1() {
            InitializeComponent();
            DoubleBuffered = true;
            consoleHelper = new ConsoleHelper();
            splitContainer1.SplitterDistance = (int)(Height * 0.6);
            splitContainer2.SplitterDistance = (int)(Width * 0.7);
            splitContainer3.SplitterDistance = (int)(Width * 0.7);
        }

        public bool Running {
            get { return running; }
            set {
                running = value;
                Text = $@"Synacor VM {(running ? "[Running]" : string.Empty)}";
            }
        }

        private void Form1_Load(object sender, EventArgs e) {
            ListViewHelper.EnableDoubleBuffer(codeView);
            ListViewHelper.EnableDoubleBuffer(registryView);
            ListViewHelper.EnableDoubleBuffer(memoryView);
            ListViewHelper.EnableDoubleBuffer(stackView);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            model?.Pause();
            consoleHelper.Dispose();
        }

        void LoadTestFile() {
            int[] testFile = {9, 32768, 32769, 4, 19, 32768, 2, 31337, 19, 65, 20, 100};
            LoadNewData(testFile);
        }

        void ClearAll() {
            if (model != null) {
                model.InstructionsView.ListChanged -= InstructionsView_ListChanged;
                model.OnViewEIPChanged -= ModelOnViewEIPChanged;
                model.OnHalt -= Model_OnHalt;
                model.OnMemoryChanged -= Model_OnMemoryChanged;
                model.OnRegistryChanged -= Model_OnRegistryChanged;
                model.OnStackPoped -= Model_OnStackPoped;
                model.OnStackPushed -= Model_OnStackPushed;
                model.OnMethodCalled -= Model_OnMethodCalled;
                model.OnMethodReturn -= Model_OnMethodReturn;
                model.OnStackReset -= Model_OnStackReset;
                model.OnBreakpointToggle -= Model_OnBreakpointToggle;
            }
            codeView.Items.Clear();
            memoryView.ByteProvider = null;
            stackView.ByteProvider = null;
            if (!System.Diagnostics.Debugger.IsAttached) // fuck VS2017
                Console.Clear();
            callStackView.Items.Clear();
        }

        void AddBindings() {
            model.InstructionsView.ListChanged += InstructionsView_ListChanged;
            model.OnViewEIPChanged += ModelOnViewEIPChanged;
            model.OnHalt += Model_OnHalt;
            model.OnMemoryChanged += Model_OnMemoryChanged;
            model.OnRegistryChanged += Model_OnRegistryChanged;
            model.OnStackPushed += Model_OnStackPushed;
            model.OnStackPoped += Model_OnStackPoped;
            model.OnMethodCalled += Model_OnMethodCalled;
            model.OnMethodReturn += Model_OnMethodReturn;
            model.OnStackReset += Model_OnStackReset;
            model.OnBreakpointToggle += Model_OnBreakpointToggle;
            memoryView.ByteProvider = new DynamicByteProvider(new byte[Model.MemorySize * 2]);
            stackView.ByteProvider = new DynamicByteProvider(new byte[0]);
        }

        private void Model_OnBreakpointToggle(object sender, BreakpointToggleEventArgs e) {
            if (e.Enable) {
                bpView.Items.Add(e.Position.ToString("X4"));
            }
            else {
                bpView.Items.Remove(e.Position.ToString("X4"));
            }
        }

        private void Model_OnStackReset(object sender, StackResetEventArgs e) {
            stackView.ByteProvider.DeleteBytes(0, stackView.ByteProvider.Length);
            for (int index = e.Stack.Length - 1; index >= 0; index--) {
                int i = e.Stack[index];
                byte[] bytes = BitConverter.GetBytes((ushort) i);
                long l = stackView.ByteProvider.Length;
                stackView.ByteProvider.InsertBytes(l, new[] {bytes[1], bytes[0]});
            }
            stackView.Invalidate();

            callStackView.Items.Clear();
            for (int index = e.CallStack.Length - 1; index >= 0; index--) {
                int i = e.CallStack[index];
                callStackView.Items.Insert(0, i.ToString("X4"));
            }
        }

        private void Model_OnMethodReturn(object sender, EventArgs e) {
            callStackView.Items.RemoveAt(0);
        }

        private void Model_OnMethodCalled(object sender, MethodCalledEventArgs e) {
            callStackView.Items.Insert(0, e.RetIP.ToString("X4"));
        }

        private void Model_OnStackPoped(object sender, EventArgs e) {
            long l = stackView.ByteProvider.Length;
            stackView.ByteProvider.DeleteBytes(l - 2, 2);
            stackView.Invalidate();
        }

        private void Model_OnStackPushed(object sender, StackPushedEventArgs e) {
            byte[] bytes = BitConverter.GetBytes((ushort) e.Value);
            long l = stackView.ByteProvider.Length;
            stackView.ByteProvider.InsertBytes(l, new[] {bytes[1], bytes[0]});
            stackView.Invalidate();
        }

        private void Model_OnRegistryChanged(object sender, MemoryChangedEventArgs e) {
            registryView.Items[e.Position].SubItems[1].Text = e.NewValue.ToString("X4");
        }

        private void Model_OnMemoryChanged(object sender, MemoryChangedEventArgs e) {
            byte[] bytes = BitConverter.GetBytes((ushort) e.NewValue);
            memoryView.ByteProvider.WriteByte(e.Position * 2, bytes[0]);
            memoryView.ByteProvider.WriteByte(e.Position * 2 + 1, bytes[1]);
            memoryView.Invalidate();
        }

        private void Model_OnHalt(object sender, EventArgs e) {
            halted = true;
        }

        private void ModelOnViewEIPChanged(object sender, ChangedEIPEventArgs e) {
            ResetEIPMarker(e.OldValue);
            SetupEIPMarker(e.NewValue);
        }

        void SetupEIPMarker(int value) {
            if(value >= codeView.Items.Count)
                return;
            codeView.Items[value].ForeColor = eipColor;
            codeView.Items[value].EnsureVisible();
        }

        void ResetEIPMarker(int value) {
            if (value >= codeView.Items.Count)
                return;
            codeView.Items[value].ForeColor = Color.Black;
        }

        private void InstructionsView_ListChanged(object sender, ListChangedEventArgs e) {
            //ResetEIPMarker(model.EIP);
            BindingList<InstructionView> list = (BindingList<InstructionView>) sender;
            InstructionView instructionView;
            switch (e.ListChangedType) {
                case ListChangedType.Reset:
                    codeView.BeginUpdate();
                    codeView.Items.Clear();
                    foreach (InstructionView iv in list) {
                        codeView.Items.Add(ListViewItemByInstructionView(iv));
                    }
                    codeView.EndUpdate();
                    break;
                case ListChangedType.ItemAdded:
                    instructionView = list[e.NewIndex];
                    codeView.Items.Insert(e.NewIndex, ListViewItemByInstructionView(instructionView));
                    break;
                case ListChangedType.ItemDeleted:
                    codeView.Items.RemoveAt(e.NewIndex);
                    break;
                case ListChangedType.ItemMoved:
                    throw new NotImplementedException();
                case ListChangedType.ItemChanged:
                    if (e.PropertyDescriptor.Name == "InstructionInfo") {
                        instructionView = list[e.NewIndex];
                        codeView.Items[e.NewIndex].SubItems[2].Text = instructionView.InstructionInfo;
                        break;
                    }
                    throw new NotImplementedException();
                case ListChangedType.PropertyDescriptorAdded:
                    throw new NotImplementedException();
                case ListChangedType.PropertyDescriptorDeleted:
                    throw new NotImplementedException();
                case ListChangedType.PropertyDescriptorChanged:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
            //SetupEIPMarker(model.EIP);
        }

        static ListViewItem ListViewItemByInstructionView(InstructionView instructionView) {
            return new ListViewItem(new[] {instructionView.PositionText, instructionView.InstructionText, instructionView.InstructionInfo});
        }

        void LoadVM() {
            byte[] bytes = File.ReadAllBytes("challenge.bin");
            List<int> data = new List<int>();
            for (int i = 0; i < bytes.Length; i += 2) {
                data.Add(BitConverter.ToUInt16(bytes, i));
            }
            LoadNewData(data.ToArray());
        }

        void LoadNewData(int[] data) {
            halted = false;
            ClearAll();
            Console.Out.NewLine = "\r";
            StreamReader sr = new StreamReader(Console.OpenStandardInput());
            consoleHelper.StandardOutput.AutoFlush = true;
            //Console.SetIn(sr);
            //Console.SetOut(sw);
            model = new Model(sr, consoleHelper.StandardOutput);
            AddBindings();
            codeView.BeginUpdate();
            model.Init(data);
            codeView.EndUpdate();
            ModelOnViewEIPChanged(this, new ChangedEIPEventArgs(model.ViewEIP, model.ViewEIP));
        }

        private void toolStripButton1_Click(object sender, EventArgs e) {
            LoadTestFile();
        }

        private void toolStripButton2_Click(object sender, EventArgs e) {
            LoadVM();
        }

        void Continue() {
            if (Running)
                return;
            Running = true;
            new Thread(() => {
                if (model == null)
                    return;
                model.Continue();
                Invoke((MethodInvoker) delegate {
                    Running = false;
                    model.UpdateView();
                });
            }).Start();
        }

        void StepInto() {
            model?.StepInto();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e) {
            if(e.KeyCode == Keys.F11) {
                if(!halted)
                    StepInto();
            }
            if(e.KeyCode == Keys.F5) {
                if(!halted)
                    Continue();
            }
            if (e.KeyCode == Keys.F10) {
                StepOver();
            }
            if (e.KeyCode == Keys.C && e.Control) {
                if (codeView.Focused) {
                    StringBuilder sb = new StringBuilder();
                    foreach (ListViewItem codeViewSelectedItem in codeView.SelectedItems) {
                        sb.AppendLine($"{codeViewSelectedItem.SubItems[0].Text} {codeViewSelectedItem.SubItems[1].Text}");
                    }
                    Clipboard.SetText(sb.ToString());
                }
                if(registryView.Focused) {
                    StringBuilder sb = new StringBuilder();
                    foreach(ListViewItem codeViewSelectedItem in registryView.SelectedItems) {
                        sb.AppendLine($"{codeViewSelectedItem.SubItems[0].Text} {codeViewSelectedItem.SubItems[1].Text}");
                    }
                    Clipboard.SetText(sb.ToString());
                }
                if (memoryView.Focused) {
                    memoryView.CopyHex();
                }
            }
        }

        private void toolStripButton8_Click(object sender, EventArgs e) {
            Pause();
        }

        void Pause() {
            if (model == null)
                return;
            model.Pause();
            Running = false;
        }

        private void toolStripButton3_Click(object sender, EventArgs e) {
            if (!halted)
                Continue();
        }

        private void toolStripButton9_Click(object sender, EventArgs e) {
            Stop();
        }

        void Stop() {
            if (model == null)
                return;
            model.Stop();
            Running = false;
        }

        private void toolStripButton4_Click(object sender, EventArgs e) {
            StepInto();
        }

        private void toolStripButton5_Click(object sender, EventArgs e) {
            StepOver();
        }

        void StepOver() {
            model?.StepOver();
        }

        private void toolStripButton6_Click(object sender, EventArgs e) {
            if (running)
                return;
            SetBreakpointForm setBreakpointForm = new SetBreakpointForm();
            if (codeView.FocusedItem != null)
                setBreakpointForm.ipTxb.Text = $@"0x{codeView.FocusedItem.Text}";
            if (setBreakpointForm.ShowDialog() != DialogResult.OK)
                return;
            model.ToggleBreakpoint(setBreakpointForm.ipTxb.Text, setBreakpointForm.executeChb.Checked, setBreakpointForm.writeCkb.Checked, setBreakpointForm.readCkb.Checked);
        }

        private void toolStripButton10_Click(object sender, EventArgs e) {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                model.SaveState(saveFileDialog1.FileName);
            }
        }

        private void toolStripButton11_Click(object sender, EventArgs e) {
            if(openFileDialog1.ShowDialog() == DialogResult.OK) {
                model.LoadState(openFileDialog1.FileName);
            }
        }

        private void memoryView_KeyDown(object sender, KeyEventArgs e) {
            if(e.KeyCode == Keys.D && e.Control) {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < memoryView.SelectionLength; i++) {
                    long p = memoryView.SelectionStart + i;
                    sb.Append(memoryView.ByteProvider.ReadByte(p).ToString("X2"));
                }
                Clipboard.SetText(sb.ToString());
            }
        }
    }
}