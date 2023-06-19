using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Mail;
using Mail;


namespace Main
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            dgv.Columns.Add("1", "От");
            dgv.Columns.Add("2", "Тема");
            dgv.Columns.Add("3", "Время");

        }
        Mail.Client smtp;
        Mail.Client pop;

        private void Auth()
        {
            pop = new Client();
            pop.Console(console);

            pop.RunClient("pop.yandex.ru", 995);

            pop.AuthorizationPOP(password.Text, login.Text);
            pb.Visible = true;
        }
        private void connect_Click(object sender, EventArgs e)
        {
            Auth();
            pb.Visible = true;
            UpdateTable();            
        }
        private void UpdateTable()
        {
            messageBox.Text = "";

            while (dgv.Rows.Count > 0)
                dgv.Rows.Remove(dgv.Rows[0]);


            pop.GetCountMail();
            int a = pop.count;

            pb.Maximum = a;

            for (int i = 0; i < a; i++)
            {
                pb.Value = i + 1;
                pop.GetMessageAsBytes(i + 1, false);
                pop.GetHeaders();
                dgv.Rows.Add();

                if (pop.headers.ContainsKey("From"))
                    dgv.Rows[i].Cells[0].Value = pop.headers["From"];
                if (pop.headers.ContainsKey("Subject"))
                    dgv.Rows[i].Cells[1].Value = pop.headers["Subject"];
                if (pop.headers.ContainsKey("DATE"))
                    dgv.Rows[i].Cells[2].Value = pop.headers["Date"];
            }
         
            pb.Visible = false;
        }
        int index = -1;
        private void dgv_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            index = e.RowIndex;
            pop.GetMessageAsBytes(e.RowIndex + 1, false);
            pop.GetData();
            messageBox.Clear();

            for (int i = 0; i < pop.data.Count; i++)
            {
                messageBox.Text += pop.data[i];
            }
        }

        private void send_Click(object sender, EventArgs e)
        {
            smtp = new Client();
            smtp.Console(console);
            smtp.RunClient("smtp.yandex.ru", 465);
            smtp.AuthorizationSMTP(password.Text, login.Text);

            smtp.SendCommand("HELO server");
            smtp.SendCommand("MAIL FROM: "+ ot.Text + " SIZE="+text.Text.Length);
            smtp.SendCommand("RCPT TO: <" + komu.Text + ">");
            smtp.SendCommand("DATA \r\nFrom: "  + login.Text + "\r\nSubject: " + tema.Text + "\r\n\r\n"+text.Text);

            smtp.SendCommand(".");
            smtp.SendCommand("QUIT");
        }

        private void dgv_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
        {
            pop.Delete(index + 1);
            pop.Close();
            Auth();
            UpdateTable();
        }
    }
}
