using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Hospital.Business;
using Hospital.Services;

namespace Hospital
{
    /// <summary>Presentation Layer: знает только про HospitalService. Все элементы создаются кодом — дизайнер не нужен.</summary>
    public class MainForm : Form
    {
        private readonly HospitalService _service;
        private readonly ListBox lstPatients = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, FormattingEnabled = true };
        private readonly ListView lvRx = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false };
        private readonly Label lblInfo = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(4), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        private readonly ComboBox cmbStaff = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, DisplayMember = "Name" };

        public MainForm(HospitalService service)
        {
            _service = service;
            Text = "Больница";
            Width = 1000; Height = 600; StartPosition = FormStartPosition.CenterScreen;

            // --- левая панель: список пациентов + госпитализация ---
            var btnAdmit = new Button { Text = "Госпитализировать…", Dock = DockStyle.Bottom, Height = 36 };
            var left = new Panel { Dock = DockStyle.Left, Width = 330 };
            left.Controls.Add(lstPatients);
            left.Controls.Add(btnAdmit);
            left.Controls.Add(new Label { Text = "Пациенты в стационаре", Dock = DockStyle.Top, Height = 24 });

            // --- правая панель: ЭМК (назначения) + действия ---
            lvRx.Columns.Add("Выполнено", 80);
            lvRx.Columns.Add("Назначение", 330);
            lvRx.Columns.Add("Исполнитель", 150);
            lvRx.Columns.Add("Когда", 110);

            var btnPrescribe = new Button { Text = "Назначить…", AutoSize = true };
            var btnPerform = new Button { Text = "Выполнить выбранное", AutoSize = true };
            var btnDischarge = new Button { Text = "Выписать", AutoSize = true };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(4) };
            actions.Controls.Add(btnPrescribe);
            actions.Controls.Add(new Label { Text = "Выполняет:", AutoSize = true, Padding = new Padding(10, 6, 0, 0) });
            actions.Controls.Add(cmbStaff);
            actions.Controls.Add(btnPerform);
            actions.Controls.Add(btnDischarge);

            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(lvRx);
            right.Controls.Add(actions);
            right.Controls.Add(lblInfo);

            Controls.Add(right);
            Controls.Add(left);

            // --- обработчики ---
            lstPatients.Format += (s, e) => e.Value = _service.Title((MedicalRecord)e.ListItem);
            lstPatients.SelectedIndexChanged += (s, e) => ShowRecord();
            btnAdmit.Click += (s, e) => { if (AdmitDialog()) Reload(); };
            btnPrescribe.Click += (s, e) =>
            {
                var r = Selected;
                if (r != null && PrescribeDialog(r)) ShowRecord();
            };
            btnPerform.Click += (s, e) => Perform();
            btnDischarge.Click += (s, e) => Discharge();

            Fill(cmbStaff, _service.AllStaff);
            Reload();
        }

        private MedicalRecord Selected { get { return lstPatients.SelectedItem as MedicalRecord; } }

        private void Reload()
        {
            lstPatients.Items.Clear();
            foreach (var r in _service.ActiveRecords) lstPatients.Items.Add(r);
            if (lstPatients.Items.Count > 0) lstPatients.SelectedIndex = 0;
            ShowRecord();
        }

        private void ShowRecord()
        {
            lvRx.Items.Clear();
            var r = Selected;
            if (r == null) { lblInfo.Text = "Выберите пациента или госпитализируйте нового."; return; }
            lblInfo.Text = _service.Title(r) + "\nДиагноз при поступлении: " + r.Diagnosis;
            foreach (var p in r.Prescriptions)
            {
                var item = new ListViewItem(new[]
                {
                    p.Done ? "✔" : "", p.ToString(), p.DoneBy ?? "",
                    p.DoneAt.HasValue ? p.DoneAt.Value.ToString("dd.MM HH:mm") : ""
                });
                item.Tag = p;
                lvRx.Items.Add(item);
            }
        }

        private void Perform()
        {
            var r = Selected;
            if (r == null || lvRx.SelectedItems.Count == 0) { Warn("Выберите назначение."); return; }
            try
            {
                _service.Perform(r, (Prescription)lvRx.SelectedItems[0].Tag, (Staff)cmbStaff.SelectedItem);
                ShowRecord();
            }
            catch (InvalidOperationException ex) { Warn(ex.Message); }
        }

        private void Discharge()
        {
            var r = Selected;
            if (r == null) return;
            if (MessageBox.Show("Выписать пациента?", "Выписка", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            MessageBox.Show(_service.Discharge(r), "Выписной эпикриз");
            Reload();
        }

        // ================= диалоги =================
        private bool AdmitDialog()
        {
            TableLayoutPanel t;
            var f = MakeDialog("Госпитализация", out t);
            var header = new Label { Text = "Первичный осмотр: " + _service.DutyDoctor.Name, AutoSize = true };
            t.Controls.Add(header);
            t.SetColumnSpan(header, 2);

            var name = Row(t, "ФИО", new TextBox());
            var birth = Row(t, "Дата рождения", new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddYears(-30) });
            var complaint = Row(t, "Жалобы", new TextBox());
            var diagnosis = Row(t, "Диагноз", new TextBox());
            var dept = Row(t, "Отделение", Fill(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" }, _service.Departments));
            var doctor = Row(t, "Лечащий врач", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" });

            Action fillDoctors = () => Fill(doctor, _service.DoctorsOf(((Department)dept.SelectedItem).Id));
            dept.SelectedIndexChanged += (s, e) => fillDoctors();
            fillDoctors();

            return Confirm(f, t, () =>
            {
                var p = new Patient { Name = name.Text, BirthDate = birth.Value, Complaint = complaint.Text };
                var d = (Staff)doctor.SelectedItem;
                if (d == null) throw new ArgumentException("В отделении нет врача.");
                _service.Admit(p, diagnosis.Text, ((Department)dept.SelectedItem).Id, d.Id);
            });
        }

        private bool PrescribeDialog(MedicalRecord r)
        {
            TableLayoutPanel t;
            var f = MakeDialog("Назначение", out t);
            var kind = Row(t, "Вид", Fill(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, (PrescriptionKind[])Enum.GetValues(typeof(PrescriptionKind))));
            var desc = Row(t, "Название", new TextBox());
            var form = Row(t, "Форма", Fill(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, (MedForm[])Enum.GetValues(typeof(MedForm))));
            var qty = Row(t, "Инъекций (шт.)", new NumericUpDown { Minimum = 0, Maximum = 100 });
            var dose = Row(t, "Дозировка (мг)", new NumericUpDown { Minimum = 0, Maximum = 5000, DecimalPlaces = 1 });
            var days = Row(t, "Дней приёма", new NumericUpDown { Minimum = 0, Maximum = 365 });

            Action toggle = () =>   // доступны только нужные поля
            {
                bool med = (PrescriptionKind)kind.SelectedItem == PrescriptionKind.Medication;
                bool inj = (MedForm)form.SelectedItem == MedForm.Injection;
                form.Enabled = med;
                qty.Enabled = med && inj;
                dose.Enabled = days.Enabled = med && !inj;
            };
            kind.SelectedIndexChanged += (s, e) => toggle();
            form.SelectedIndexChanged += (s, e) => toggle();
            toggle();

            return Confirm(f, t, () =>
            {
                var k = (PrescriptionKind)kind.SelectedItem;
                var p = new Prescription { Kind = k, Description = desc.Text };
                if (k == PrescriptionKind.Medication)
                {
                    p.Form = (MedForm)form.SelectedItem;
                    p.Quantity = (int)qty.Value;
                    p.DosageMg = (double)dose.Value;
                    p.Days = (int)days.Value;
                }
                _service.Prescribe(r, p);
            });
        }

        // ================= помощники =================
        private static Form MakeDialog(string title, out TableLayoutPanel table)
        {
            var f = new Form
            {
                Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false, MinimizeBox = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10)
            };
            table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            f.Controls.Add(table);
            return f;
        }

        private static T Row<T>(TableLayoutPanel t, string label, T c) where T : Control
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 10, 3) });
            c.Width = 260;
            t.Controls.Add(c);
            return c;
        }

        /// <summary>Заполняет комбобокс без DataSource (у не показанной формы DataSource не выставляет SelectedItem).</summary>
        private static ComboBox Fill<T>(ComboBox c, IEnumerable<T> items)
        {
            c.Items.Clear();
            foreach (var i in items) c.Items.Add(i);
            if (c.Items.Count > 0) c.SelectedIndex = 0;
            return c;
        }

        /// <summary>Кнопка OK выполняет действие; при ошибке валидации показывает сообщение и не закрывает окно.</summary>
        private bool Confirm(Form f, TableLayoutPanel t, Action action)
        {
            var ok = new Button { Text = "OK", Width = 90 };
            ok.Click += (s, e) =>
            {
                try { action(); f.DialogResult = DialogResult.OK; }
                catch (ArgumentException ex) { Warn(ex.Message); }
            };
            t.Controls.Add(new Label());
            t.Controls.Add(ok);
            f.AcceptButton = ok;
            return f.ShowDialog(this) == DialogResult.OK;
        }

        private static void Warn(string text)
        {
            MessageBox.Show(text, "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
