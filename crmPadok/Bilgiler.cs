﻿using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Excel = Microsoft.Office.Interop.Excel;
using System.Net;
using System.IO;
using System.Text;
using System.Linq;

namespace crmPadok
{
    public enum TelTaskStatus
    {
        Waiting,
        Running,
        Cancelled,
        Completed,
    };
    public enum AdslTaskStatus
    {
        Waiting,
        Running,
        Cancelled,
        Completed,
    };
    public partial class Bilgiler : Form
    {
        
        public TelTaskStatus telStatus=TelTaskStatus.Waiting;

        public AdslTaskStatus adslStatus=AdslTaskStatus.Waiting;

        List<Faturalar> telFaturaListe;

        List<Faturalar> adslFaturaListe;

        Crm objCrm;

        CancellationTokenSource sourceTel;

        CancellationToken tokenTel;

        CancellationTokenSource sourceAdsl;

        CancellationToken tokenAdsl;

        public Bilgiler(Crm objCrm)
        {
            InitializeComponent();
            this.objCrm = objCrm;
        }
        private async Task<List<Faturalar>> getTelFatura()
        {
           
            object state = new object();

            telFaturaListe = new List<Faturalar>();
            List<Task> taskList = new List<Task>();
            sourceTel = new CancellationTokenSource();
            tokenTel = new CancellationToken();
            tokenTel = sourceTel.Token;
            string[] numaralar = txtTelefon.Text.Split('\n');

            if(txtTelefon.Text.Length>9)
              telStatus = TelTaskStatus.Running;

            float progress = (float)100 / (float)numaralar.Length;
            Dictionary<string, string> keyList = objCrm.getHesapNo();
            if(keyList.Count<15)
            {
                MessageBox.Show("Oturumunuz kapatılmıştır lütfen oturum açınız");
                return null;
            }
            int i = 0;
            foreach (var numara in numaralar)
            {
                if (numara == "")
                    continue;
                Sorgula objSorgula = new Sorgula();
                //objSorgula.Container = objCrm.Container;
                objSorgula.Container = objCrm.Container;
                objSorgula.List = keyList;
                var sonTask = Task.Run(() => objSorgula.telefonFatura(numara,tokenTel), tokenTel).ContinueWith(async (t) =>
                  {
                      string telNo = numara;
                      await t;
                      i++;
                      progressBar1.Value =Convert.ToInt32(Math.Ceiling(Convert.ToDouble(progress * i)));
                      if (t.IsFaulted)
                          txtSonuclar.Text += telNo + "=>faulted " + t.Exception.Message;
                      else
                      {
                        lock (state)
                        {

                            Faturalar telFatura = t.Result;
                              if (telFatura != null)
                              {
                                  Task.Factory.StartNew(() => printToScreen(telFatura.ToString() + "\n"), tokenTel, TaskCreationOptions.AttachedToParent, TaskScheduler.FromCurrentSynchronizationContext());
                                  telFaturaListe.Add(telFatura);
                              }
                              else if (t.IsCanceled)
                                  Task.Factory.StartNew(() => printToScreen("Cancelled"));
                              else
                              {
                                  Task.Factory.StartNew(() => printToScreen(telNo + "=> --------------\n"), tokenTel, TaskCreationOptions.AttachedToParent, TaskScheduler.FromCurrentSynchronizationContext());
                                  telFaturaListe.Add(new Faturalar(telNo, "", "", ""));
                              }
                          }
                      }
                  },
                 TaskScheduler.FromCurrentSynchronizationContext());

                taskList.Add(sonTask);

                if (taskList.Count % 100 == 0)
                    await Task.WhenAll(taskList);
            }
            await Task.WhenAll(taskList);
            if (progressBar1.Value == 100)
            {
                MessageBox.Show("Sorgulama tamamlandı", "Padok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                telStatus = TelTaskStatus.Completed;
            }
            else if(tokenTel.IsCancellationRequested)
            {
                telStatus = TelTaskStatus.Cancelled;
                MessageBox.Show("İptal edildi.", "Padok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progressBar1.Value = 0;
            }
            else
            {
                telStatus = TelTaskStatus.Waiting;
            }
            return telFaturaListe;
        }
        private async Task<List<Faturalar>> getAdslFatura()
        {
            object state = new object();
            adslStatus = AdslTaskStatus.Running;
            adslFaturaListe = new List<Faturalar>();
            List<Task> taskList = new List<Task>();
            sourceAdsl = new CancellationTokenSource();
            tokenAdsl = new CancellationToken();
            tokenAdsl = sourceAdsl.Token;
            string[] numaralar = txtAdsl.Text.Split('\n');
            float progress = (float)100 / (float)numaralar.Length;
            Dictionary<string, string> keyList = objCrm.getHesapNo();
            int i = 0;
            foreach (var numara in numaralar)
            {
                if (numara == "")
                    continue;
                //Her sorgulama için ayrı obje
                Sorgula objSorgula = new Sorgula();
                //daha önceki cookieler yeni objeye aktarılıyor
                objSorgula.Container = objCrm.Container ;
                objSorgula.List = keyList;
                var sonTask = Task.Run(() => objSorgula.adslFatura(numara,tokenAdsl), tokenAdsl).ContinueWith(async (t) =>
                {
                    string adslNo = numara;

                    await t;
                    
                    if (t.IsFaulted)
                        txtAdslSonuc.Text += adslNo + "=>faulted " + t.Exception.Message;
                    else
                    {
                        lock (state)
                        {
                            i++;
                            prgAdsl.Value = Convert.ToInt32(progress * i);
                            Faturalar adslFatura = t.Result;
                            if (adslFatura != null)
                            {
                                Task.Factory.StartNew(() => printToScreenAdsl(adslFatura.ToString() + "\n"), tokenAdsl, TaskCreationOptions.AttachedToParent, TaskScheduler.FromCurrentSynchronizationContext());
                                adslFaturaListe.Add(adslFatura);
                            }
                            else if (t.IsCanceled)
                                Task.Factory.StartNew(() => printToScreenAdsl("Cancelled"));
                            else
                            {
                                Task.Factory.StartNew(() => printToScreenAdsl(adslNo + "=> --------------\n"), tokenAdsl, TaskCreationOptions.AttachedToParent, TaskScheduler.FromCurrentSynchronizationContext());
                                adslFaturaListe.Add(new Faturalar(adslNo, "", "", ""));
                            }
                        }
                    }
                },
                 TaskScheduler.FromCurrentSynchronizationContext());

                taskList.Add(sonTask);
                if (taskList.Count % 100 == 0)
                    await Task.WhenAll(taskList);
            }
            //sorgulama bitti yada iptal edildi
            await Task.WhenAll(taskList);
            if (prgAdsl.Value == 100)
            {
                MessageBox.Show("Sorgulama tamamlandı", "Padok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                adslStatus = AdslTaskStatus.Completed;
            }
            else if (tokenAdsl.IsCancellationRequested)
            {
                adslStatus = AdslTaskStatus.Cancelled;
                MessageBox.Show("İptal edildi.", "Padok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                prgAdsl.Value = 0;
            }
            else
            {
                adslStatus = AdslTaskStatus.Waiting;
            }
            return adslFaturaListe;
        }
        private void printToScreen(string text)
        {
            txtSonuclar.Text += text;
        }
        private void printToScreenAdsl(string text)
        {
            txtAdslSonuc.Text += text;
        }
        private bool oturumKontrol()
        {
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
        private async void btnTelefon_Click(object sender, EventArgs e)
        {
            if (!oturumKontrol())
            {
                MessageBox.Show("Oturumunuz kapatılmış yeniden oturum açınız");
                this.Hide();
                Form f = Application.OpenForms["AnaForm"];
                AnaForm form = (AnaForm)f;
                form.Show();
                return;
            }
            if (telStatus == TelTaskStatus.Running)
            {
                MessageBox.Show("Zaten çalışan bir sorgulama işlemi var.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            List<Faturalar> telList;
            
                telList = await getTelFatura();
        }
        private async void btnSorgula_Click(object sender, EventArgs e)
        {

            if (!oturumKontrol())
            {
                MessageBox.Show("Oturumunuz kapatılmış yeniden oturum açınız");
                this.Hide();
                Form f = Application.OpenForms["AnaForm"];
                AnaForm form = (AnaForm)f;
                form.Show();
                return;
            }
            if (adslStatus == AdslTaskStatus.Running)
            {
                MessageBox.Show("Zaten çalışan bir sorgulama işlemi var.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            List<Faturalar> telList = await getAdslFatura();

            //if (txtAdsl.Text.Length != 10)
            //{
            //    MessageBox.Show("Hatalı giriş yaptınız", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return;
            //}
            //string[] sonuc = objCrm.adslFatura(txtAdsl.Text); 
            //if (sonuc.Length >= 10)
            //    MessageBox.Show("İsim: " + sonuc[10] + " Borç Dönemi: " + sonuc[2] + " Borç: " + sonuc[1]);
            //else
            //    MessageBox.Show("Kayıt bulunamadı");
        }
      
        private void Bilgiler_FormClosing(object sender, FormClosingEventArgs e)
        {
            Form f = Application.OpenForms["AnaForm"];
            ((AnaForm)f).Show();
        }

        private void btnIptal_Click(object sender, EventArgs e)
        {
            if (tokenTel.CanBeCanceled && !tokenTel.IsCancellationRequested && progressBar1.Value != 0 && progressBar1.Value != 100)
            {
                DialogResult result = MessageBox.Show("İptal etmek istediğinize emin misiniz?", "iptal işlemi", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    sourceTel.Cancel(true);
                }
            }
            else
            {
                MessageBox.Show("Devam eden bir sorgulama işlemi yok", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnAdslIptal_Click(object sender, EventArgs e)
        {
            if (tokenAdsl.CanBeCanceled && !tokenAdsl.IsCancellationRequested && prgAdsl.Value != 0 && prgAdsl.Value != 100)
            {
                DialogResult result = MessageBox.Show("İptal etmek istediğinize emin misiniz?", "iptal işlemi", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    sourceAdsl.Cancel(true);
                    MessageBox.Show("İptal edildi.", "Padok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    prgAdsl.Value = 0;
                }
            }
            else
            {
                MessageBox.Show("Devam eden bir sorgulama işlemi yok", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExcel_Click(object sender, EventArgs e)
        {
            if (telStatus == TelTaskStatus.Running)
            {
                MessageBox.Show("Lütfen sorgulama işleminin bitmesini bekleyiniz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if(telStatus==TelTaskStatus.Waiting)
            {
                MessageBox.Show("Excel'de görüntüleme yapmak için sorgulama yapmalısınız.","Hata",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return;
            }
            else if(telFaturaListe == null)
                return;

            List<Faturalar> SortedList = telFaturaListe.OrderBy(o => o.AboneNo).ToList();
            DisplayInExcel(SortedList);

        }
        void DisplayInExcel(List<Faturalar> faturalar)
        {
            var excelApp = new Excel.Application();

            excelApp.Visible = true;

            excelApp.Workbooks.Add();

            Excel._Worksheet workSheet = excelApp.ActiveSheet;

            // Earlier versions of C# require explicit casting.
            //Excel._Worksheet workSheet = (Excel.Worksheet)excelApp.ActiveSheet;

            // Establish column headings in cells A1 and B1.
            workSheet.Cells[1, "A"] = "Telefon Numarası";
            workSheet.Cells[1, "B"] = "İsim";
            workSheet.Cells[1, "C"] = "Fatura Dönemi";
            workSheet.Cells[1, "D"] = "Fiyat";

            var row = 1;
            foreach (var fatura in faturalar)
            {
                row++;
                workSheet.Cells[row, "A"] = fatura.AboneNo;
                workSheet.Cells[row, "B"] = fatura.Isim;
                workSheet.Cells[row, "C"] = fatura.FaturaDonemi;
                workSheet.Cells[row, "D"] = fatura.Fiyat;
            }

            workSheet.Columns[1].AutoFit();
            workSheet.Columns[2].AutoFit();
            workSheet.Columns[3].AutoFit();
            workSheet.Columns[4].AutoFit();

            // Call to AutoFormat in Visual C# 2010. This statement replaces the 
            // two calls to AutoFit.
            workSheet.Range["A1", "D"+(faturalar.Count+1)].AutoFormat(
                Excel.XlRangeAutoFormat.xlRangeAutoFormatClassic1);

            // Put the spreadsheet contents on the clipboard. The Copy method has one
            // optional parameter for specifying a destination. Because no argument  
            // is sent, the destination is the Clipboard.
            //workSheet.Range["A1:B3"].Copy();
        }

        private void btnAdslExcel_Click(object sender, EventArgs e)
        {
            if (adslStatus == AdslTaskStatus.Running)
            {
                MessageBox.Show("Lütfen sorgulama işleminin bitmesini bekleyiniz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (adslStatus == AdslTaskStatus.Waiting)
            {
                MessageBox.Show("Excel'de görüntüleme yapmak için sorgulama yapmalısınız.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (adslFaturaListe == null)
                return;

            List<Faturalar> SortedList = adslFaturaListe.OrderBy(o => o.AboneNo).ToList();
            DisplayInExcel(SortedList);
        }
    }
}
