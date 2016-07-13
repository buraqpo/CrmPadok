﻿using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;

namespace crmPadok
{
    public partial class AnaForm : Form
    {
        Crm objCrm = new Crm();
        public AnaForm()
        {
            InitializeComponent();
        }
        private bool oturumKontrol()
        {
            string path = Application.StartupPath;
            string[] cookieFile = File.ReadAllText(path + "\\cookiefile.txt").Split(' ');
            if (cookieFile.Length <= 5)
                return false;
            Cookie cook = new Cookie(cookieFile[0], cookieFile[1], cookieFile[2], cookieFile[3]);
            Cookie cook2 = new Cookie(cookieFile[4], cookieFile[5], cookieFile[6], cookieFile[7]);
            CookieContainer newContainer = new CookieContainer();
            newContainer.Add(cook);
            newContainer.Add(cook2);
            objCrm.Container = newContainer;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://ipc2.ptt.gov.tr/pttwebapproot/ipcservlet?cmd=kurumtahsilatgiristelefon");
            req.CookieContainer = objCrm.Container;
            req.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";
            string text = "";
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                text = new StreamReader(resp.GetResponseStream(), Encoding.Default).ReadToEnd();
                if (text.Contains("secilenHesapNumarasi"))
                    return true;
                else
                    return false;
            }
        }
     
        private async void btnLogin_Click(object sender, EventArgs e)
        {
            //önceki cookie ile giriş yapılabiliniyor mu kontrol eder giriş başarılı ise yeniden sms şifresi göndermez.
            if (oturumKontrol())
            {
                MessageBox.Show("Giris basarili");
                Bilgiler bForm = new Bilgiler(objCrm);
                bForm.Show();
                this.Hide();
                return;
            }
            else
            {
                DialogResult result = MessageBox.Show("Bu cookie ile giriş sağlanamadı yeni sms gönderilecek devam etmek ister misiniz?","Uyarı",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
                if (result == DialogResult.No)
                    return;
            }

            Task<bool> taskSonuc;
             
            if (txtMusteriNo.Text.Length != 11 || txtSifre.Text.Length != 8)
            {
                MessageBox.Show("Müsteri no 11,şifre 8 karakter uzunluğunda olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            taskSonuc = Task.Run(() => objCrm.login(txtMusteriNo.Text, txtSifre.Text));
            lblBekle.Text = "Giriş yapılıyor...";
            btnLogin.Enabled = false;
            bool sonuc = await taskSonuc;
            if (sonuc)
            {
                #region gizle göster
                lblBekle.Visible = false;
                lblSifre.Visible = false;
                lblMusteri.Visible = false;
                txtMusteriNo.Visible = false;
                txtSifre.Visible = false;
                btnLogin.Visible = false;
                btnSms.Visible = true;
                btnSms.Enabled = true;
                txtSms.Visible = true;
                lblSms.Visible = true;
                txtSifre.Focus();
                #endregion
                timer();
            }
            else
            {
                btnLogin.Enabled = true;
                lblBekle.Text = "Giriş başarısız";
                MessageBox.Show("Giriş yapılırken bir hata oluştu", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private async void btnSms_Click(object sender, EventArgs e)
        {
            int smsSifreKontrol = 0;
            if(!int.TryParse(txtSms.Text,out smsSifreKontrol))
            {
                MessageBox.Show("Sms şifresi sadece rakamlardan oluşabilir", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (txtSms.Text == "" || txtSms.Text.Length!=6)
            {
                MessageBox.Show("Sms şifre uzunluğu 6 karakter olmalıdır", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Task<bool> taskSonuc = Task.Run(() => objCrm.SmsApproval(txtSms.Text));
            btnSms.Enabled = false;
            lblBekle.Text = "Giriş yapılıyor...";
            bool sonuc = await taskSonuc;
            if (sonuc)
            {
                try
                {
                    //cookieler gelmeden önce bu çalıştırılmak zorunda yok girmiyor
                    Dictionary<string, string> keyList = objCrm.getHesapNo();
                    CookieCollection cookies = objCrm.Container.GetCookies(new Uri("https://ipc2.ptt.gov.tr/pttwebapproot/ipcservlet"));

                    //programın klasörü
                    string path = Application.StartupPath;
                    path += "\\cookiefile.txt";

                    string cookieValues = "";
                    //yeni cookie yi dosyaya yaz
                    foreach (Cookie cookie in cookies)
                       cookieValues+= cookie.Name + " " + cookie.Value + " " + cookie.Path + " " + cookie.Domain + " ";

                    File.WriteAllText(path, cookieValues);

                    tmr.Stop();
                    #region gizle göster
                    txtSms.Text = "";
                    txtSms.Visible = false;
                    btnSms.Visible = false;
                    lblSms.Visible = false;
                    btnLogin.Visible = true;
                    btnLogin.Enabled = true;
                    txtSifre.Visible = true;
                    txtMusteriNo.Visible = true;
                    lblMusteri.Visible = true;
                    lblSifre.Visible = true;
                    lblTime.Visible = false;
                    this.Hide(); 
                    #endregion
                    Bilgiler bForm = new Bilgiler(objCrm);
                    bForm.Show();
                }
                catch(IOException ex)
                {
                    MessageBox.Show("Cookie dosyası yazılırken bir hata oluştu",ex.Message);
                }
                catch (Exception ex)
                {
                    btnSms.Enabled = true;
                    MessageBox.Show("Bilgiler gönderilirken hata oluştu " + ex.Message);
                }
            }
            else
            {
                btnSms.Enabled = true;
                MessageBox.Show("Giriş Başarısız");
            }
        }
        //sms şifre giriş süresi 3 dakika
        private void timer()
        {
            tmr.Start();
            DateTime dt = DateTime.Now.AddHours(0).AddMinutes(3).AddSeconds(0);
            lblTime.Visible = true;
            tmr.Interval = 1000;
            tmr.Tick += (sender, e) =>
            {
                TimeSpan diff = dt.Subtract(DateTime.Now);
                lblTime.Text = string.Format("{0:00}:{1:00}:{2:00}", diff.Hours, diff.Minutes, diff.Seconds);
                if (dt < DateTime.Now)
                {
                    ((System.Windows.Forms.Timer)sender).Stop();
                    MessageBox.Show("Sms şifresi giriş süreniz doldu lütfen tekrar giriş yapınız","Uyarı",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    #region gizle göster
                    txtMusteriNo.Text = "";
                    txtSifre.Text = "";
                    txtSms.Text = "";
                    txtSms.Visible = false;
                    btnSms.Visible = false;
                    lblSms.Visible = false;
                    btnLogin.Visible = true;
                    txtSifre.Visible = true;
                    txtMusteriNo.Visible = true;
                    lblMusteri.Visible = true;
                    lblSifre.Visible = true;
                    lblTime.Visible = false;
                    btnLogin.Enabled = true;
                    btnSms.Enabled = true;
                    #endregion
                }
                   
            };
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            txtMusteriNo.Focus();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Bilgiler bilg = new Bilgiler(objCrm);
            bilg.Show();
        }
    }
}
