using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SkyjoWPF.Core;

namespace SkyjoWPF
{
    public enum ModeJeu { Solo, Hote, Client }

    public class ServeurInfo
    {
        public string IP { get; set; }
        public string NomDuPC { get; set; }
        public string NomAffichage => $"{NomDuPC} ({IP})";
    }

    public partial class MainWindow : Window
    {
        private MoteurJeu jeu;
        private Carte carteEnMain;
        private Carte carteEnMainAdversaire;
        private bool tourDuJoueur = true;

        private Carte[] MaGrille;
        private Carte[] GrilleAdversaire;
        private ModeJeu modeActuel = ModeJeu.Solo;

        private bool isHostingBroadcast = false;
        private const int PORT_UDP_SCAN = 5556;

        private TcpListener serveurTcp;
        private TcpClient clientTcp;
        private StreamReader reseauReader;
        private StreamWriter reseauWriter;
        private const int PORT_TCP_JEU = 5557;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnSolo_Click(object sender, RoutedEventArgs e)
        {
            modeActuel = ModeJeu.Solo;
            TxtNomAdversaire.Text = "ADVERSAIRE (IA)";
            LancerPartieLocale();
        }

        private async void BtnHeberger_Click(object sender, RoutedEventArgs e)
        {
            modeActuel = ModeJeu.Hote;
            TxtNomAdversaire.Text = "JOUEUR 2";
            TxtStatutMenu.Text = "Hébergement en cours... En attente d'un joueur.";

            isHostingBroadcast = true;
            _ = BroadcasterPresence();

            try
            {
                serveurTcp = new TcpListener(IPAddress.Any, PORT_TCP_JEU);
                serveurTcp.Start();
                clientTcp = await serveurTcp.AcceptTcpClientAsync();
                isHostingBroadcast = false;

                InitialiserConnexionReseau();
                MessageBox.Show("Un joueur a rejoint votre partie !");

                LancerPartieLocale();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur d'hébergement. Le pare-feu bloque peut-être le port 5557 ?\n" + ex.Message);
            }
        }

        private async void BtnScanner_Click(object sender, RoutedEventArgs e)
        {
            TxtStatutMenu.Text = "Recherche de parties en cours...";
            ListServeurs.Items.Clear();

            using (UdpClient udp = new UdpClient())
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, PORT_UDP_SCAN));

                var receiveTask = udp.ReceiveAsync();
                var delayTask = Task.Delay(4000);

                while (true)
                {
                    var completedTask = await Task.WhenAny(receiveTask, delayTask);
                    if (completedTask == delayTask) break;

                    var result = receiveTask.Result;
                    string msg = Encoding.UTF8.GetString(result.Buffer);

                    if (msg.StartsWith("SKYJO|"))
                    {
                        string nomDuPC = msg.Split('|')[1];
                        string ip = result.RemoteEndPoint.Address.ToString();

                        bool dejaPresent = false;
                        foreach (ServeurInfo s in ListServeurs.Items) { if (s.IP == ip) dejaPresent = true; }

                        if (!dejaPresent) ListServeurs.Items.Add(new ServeurInfo { IP = ip, NomDuPC = nomDuPC });
                    }
                    receiveTask = udp.ReceiveAsync();
                }
            }
            TxtStatutMenu.Text = "Scan terminé.";
        }

        private async void BtnRejoindre_Click(object sender, RoutedEventArgs e)
        {
            if (ListServeurs.SelectedItem is ServeurInfo serveurChoisi)
            {
                modeActuel = ModeJeu.Client;
                TxtNomAdversaire.Text = $"HÔTE ({serveurChoisi.NomDuPC})";
                TxtStatutMenu.Text = "Connexion en cours...";

                try
                {
                    clientTcp = new TcpClient();
                    await clientTcp.ConnectAsync(serveurChoisi.IP, PORT_TCP_JEU);
                    InitialiserConnexionReseau();
                    MenuPanel.Visibility = Visibility.Collapsed;
                    MessageBox.Show("Connecté ! En attente du plateau de l'hôte...");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Impossible de rejoindre la partie : " + ex.Message);
                    TxtStatutMenu.Text = "Échec de la connexion.";
                }
            }
            else MessageBox.Show("Veuillez sélectionner une partie dans la liste !");
        }

        private async Task BroadcasterPresence()
        {
            using (UdpClient udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, PORT_UDP_SCAN);
                string monNom = Dns.GetHostName();
                byte[] bytes = Encoding.UTF8.GetBytes($"SKYJO|{monNom}");

                while (isHostingBroadcast)
                {
                    await udp.SendAsync(bytes, bytes.Length, endPoint);
                    await Task.Delay(2000);
                }
            }
        }

        private void InitialiserConnexionReseau()
        {
            var stream = clientTcp.GetStream();
            reseauReader = new StreamReader(stream, Encoding.UTF8);
            reseauWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            _ = EcouterReseau();
        }

        private async Task EcouterReseau()
        {
            try
            {
                while (clientTcp != null && clientTcp.Connected)
                {
                    string message = await reseauReader.ReadLineAsync();
                    if (message != null)
                    {
                        await Dispatcher.InvokeAsync(async () => await TraiterMessageReseau(message));
                    }
                }
            }
            catch (Exception)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Connexion perdue avec l'adversaire."));
            }
        }

        private void EnvoyerMessageReseau(string message)
        {
            if (clientTcp != null && clientTcp.Connected)
            {
                reseauWriter.WriteLine(message);
            }
        }

        private async Task TraiterMessageReseau(string message)
        {
            string[] parts = message.Split('|');
            string cmd = parts[0];

            if (cmd == "SYNC_INIT")
            {
                int seed = int.Parse(parts[1]);
                LancerPartieLocale(seed);
            }
            else if (cmd == "ACTION")
            {
                string typeAction = parts[1];
                if (typeAction == "PIOCHE")
                {
                    await DeplacerCurseurVers(BtnPioche);
                    carteEnMainAdversaire = jeu.TirerCarte();
                    carteEnMainAdversaire.EstVisible = true;
                }
                else if (typeAction == "PREND_DEFAUSSE")
                {
                    await DeplacerCurseurVers(BtnDefausse);
                    carteEnMainAdversaire = jeu.Defausse.Pop();
                    MettreAJourDefausse();
                }
                else if (typeAction == "REMPLACER")
                {
                    int index = int.Parse(parts[2]);
                    var conteneurBouton = ItemsGrilleIA.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                    var boutonCible = GetVisualChild<Button>(conteneurBouton);

                    if (boutonCible != null) await DeplacerCurseurVers(boutonCible);
                    await Task.Delay(300);

                    Carte ancienne = GrilleAdversaire[index];
                    ancienne.EstVisible = true;
                    jeu.Defausse.Push(ancienne);
                    GrilleAdversaire[index] = carteEnMainAdversaire;
                    carteEnMainAdversaire = null;

                    RafraichirGrilles();
                    VerifierColonnes(GrilleAdversaire);
                }
                else if (typeAction == "RETOURNER")
                {
                    int index = int.Parse(parts[2]);
                    var conteneurBouton = ItemsGrilleIA.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                    var boutonCible = GetVisualChild<Button>(conteneurBouton);

                    if (boutonCible != null) await DeplacerCurseurVers(boutonCible);
                    await Task.Delay(300);

                    GrilleAdversaire[index].EstVisible = true;
                    RafraichirGrilles();
                    VerifierColonnes(GrilleAdversaire);
                }
            }
            else if (cmd == "FIN_TOUR")
            {
                await Task.Delay(500);
                CurseurIA.Visibility = Visibility.Hidden;
                if (!PartieEstFinie(GrilleAdversaire))
                {
                    tourDuJoueur = true;
                }
            }
            else if (cmd == "FIN_PARTIE")
            {
                CurseurIA.Visibility = Visibility.Hidden;
                TerminerPartie("L'adversaire a");
            }
        }

        private void LancerPartieLocale(int seed = -1)
        {
            MenuPanel.Visibility = Visibility.Collapsed;
            jeu = new MoteurJeu(seed);

            if (modeActuel == ModeJeu.Client)
            {
                MaGrille = jeu.GrilleP2;
                GrilleAdversaire = jeu.GrilleP1;
                tourDuJoueur = false;
            }
            else
            {
                MaGrille = jeu.GrilleP1;
                GrilleAdversaire = jeu.GrilleP2;
                tourDuJoueur = true;

                if (modeActuel == ModeJeu.Hote)
                    EnvoyerMessageReseau($"SYNC_INIT|{jeu.Seed}");
            }

            RafraichirGrilles();
        }

        private void MettreAJourDefausse()
        {
            if (jeu.Defausse.Count > 0)
            {
                BtnDefausse.Content = jeu.Defausse.Peek().Valeur.ToString();

                var converter = new BrushConverter();
                BtnDefausse.Background = (Brush)converter.ConvertFromString(jeu.Defausse.Peek().CouleurFond);
                BtnDefausse.Foreground = (Brush)converter.ConvertFromString(jeu.Defausse.Peek().CouleurTexte);
            }
            else
            {
                BtnDefausse.Content = "?";
                var converter = new BrushConverter();
                BtnDefausse.Background = (Brush)converter.ConvertFromString("#ecf0f1");
                BtnDefausse.Foreground = (Brush)converter.ConvertFromString("#2c3e50");
            }
        }

        private void RafraichirGrilles()
        {
            ItemsGrille.ItemsSource = null;
            ItemsGrille.ItemsSource = MaGrille;
            ItemsGrilleIA.ItemsSource = null;
            ItemsGrilleIA.ItemsSource = GrilleAdversaire;
            MettreAJourDefausse();
        }

        private void VerifierColonnes(Carte[] grille)
        {
            for (int c = 0; c < 4; c++)
            {
                Carte c1 = grille[c]; Carte c2 = grille[c + 4]; Carte c3 = grille[c + 8];
                if (!c1.EstVide && !c2.EstVide && !c3.EstVide && c1.EstVisible && c2.EstVisible && c3.EstVisible && c1.Valeur == c2.Valeur && c2.Valeur == c3.Valeur)
                {
                    jeu.Defausse.Push(c1); c1.EstVide = true; c2.EstVide = true; c3.EstVide = true;
                }
            }
        }

        private void TerminerPartie(string declencheur)
        {
            tourDuJoueur = false;
            foreach (var c in MaGrille) c.EstVisible = true;
            foreach (var c in GrilleAdversaire) c.EstVisible = true;
            VerifierColonnes(MaGrille); VerifierColonnes(GrilleAdversaire); RafraichirGrilles();

            int scoreJoueur = MaGrille.Where(c => !c.EstVide).Sum(c => c.Valeur);
            int scoreIA = GrilleAdversaire.Where(c => !c.EstVide).Sum(c => c.Valeur);

            string message = $"{declencheur} retourné toutes ses cartes !\n\nVotre score : {scoreJoueur}\nScore IA/Adv : {scoreIA}\n\n";
            if (scoreJoueur < scoreIA) message += "🏆 VOUS AVEZ GAGNÉ !"; else if (scoreJoueur > scoreIA) message += "💀 VOUS AVEZ PERDU !"; else message += "🤝 ÉGALITÉ !";
            MessageBox.Show(message, "Fin de la partie");

            MenuPanel.Visibility = Visibility.Visible;
            isHostingBroadcast = false;
        }

        private bool PartieEstFinie(Carte[] grille) { return grille.All(c => c.EstVide || c.EstVisible); }

        private void BtnPioche_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur) return;
            if (carteEnMain == null)
            {
                carteEnMain = jeu.TirerCarte(); carteEnMain.EstVisible = true;
                if (modeActuel != ModeJeu.Solo) EnvoyerMessageReseau("ACTION|PIOCHE");
                // Le message de la pioche restauré
                MessageBox.Show($"Vous avez pioché : {carteEnMain.Valeur}\nCliquez sur une de vos cartes pour la remplacer.", "Pioche");
            }
        }

        private void BtnDefausse_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur) return;
            if (carteEnMain == null && jeu.Defausse.Count > 0)
            {
                carteEnMain = jeu.Defausse.Pop(); MettreAJourDefausse();
                if (modeActuel != ModeJeu.Solo) EnvoyerMessageReseau("ACTION|PREND_DEFAUSSE");
                // Le message de la défausse restauré
                MessageBox.Show($"Vous avez pris le {carteEnMain.Valeur} de la défausse.\nCliquez sur une de vos cartes pour la remplacer.", "Défausse");
            }
        }

        private async void Carte_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur) return;
            Button btn = sender as Button; Carte carteCliquee = btn?.DataContext as Carte;

            if (carteCliquee != null && !carteCliquee.EstVide)
            {
                if (carteEnMain != null)
                {
                    int index = Array.IndexOf(MaGrille, carteCliquee);
                    carteCliquee.EstVisible = true; jeu.Defausse.Push(carteCliquee);
                    MaGrille[index] = carteEnMain; carteEnMain = null;
                    if (modeActuel != ModeJeu.Solo) EnvoyerMessageReseau($"ACTION|REMPLACER|{index}");
                }
                else if (!carteCliquee.EstVisible)
                {
                    int index = Array.IndexOf(MaGrille, carteCliquee);
                    carteCliquee.EstVisible = true;
                    if (modeActuel != ModeJeu.Solo) EnvoyerMessageReseau($"ACTION|RETOURNER|{index}");
                }
                else return;

                RafraichirGrilles(); VerifierColonnes(MaGrille);
                if (PartieEstFinie(MaGrille))
                {
                    if (modeActuel != ModeJeu.Solo) EnvoyerMessageReseau("FIN_PARTIE");
                    TerminerPartie("Vous avez");
                    return;
                }

                if (modeActuel == ModeJeu.Solo)
                {
                    await JouerTourIA();
                }
                else
                {
                    EnvoyerMessageReseau("FIN_TOUR");
                    tourDuJoueur = false;
                }
            }
        }

        private Task DeplacerCurseurVers(UIElement cible)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (cible == null) { tcs.TrySetResult(true); return tcs.Task; }
            Point position = cible.TranslatePoint(new Point(cible.RenderSize.Width / 2 - 20, cible.RenderSize.Height / 2 - 20), OverlayCanvas);
            CurseurIA.Visibility = Visibility.Visible;
            DoubleAnimation animX = new DoubleAnimation { To = position.X, Duration = TimeSpan.FromSeconds(0.6), EasingFunction = new QuadraticEase() };
            DoubleAnimation animY = new DoubleAnimation { To = position.Y, Duration = TimeSpan.FromSeconds(0.6), EasingFunction = new QuadraticEase() };
            animX.Completed += (s, ev) => tcs.TrySetResult(true);
            CurseurIA.BeginAnimation(Canvas.LeftProperty, animX); CurseurIA.BeginAnimation(Canvas.TopProperty, animY);
            return tcs.Task;
        }

        private async Task JouerTourIA()
        {
            tourDuJoueur = false; await Task.Delay(1000);
            Carte carteChoisie = null; bool prendDefausse = false;

            if (jeu.Defausse.Count > 0 && jeu.Defausse.Peek().Valeur <= 4)
            {
                await DeplacerCurseurVers(BtnDefausse); carteChoisie = jeu.Defausse.Pop(); prendDefausse = true;
            }
            else
            {
                await DeplacerCurseurVers(BtnPioche); carteChoisie = jeu.TirerCarte(); carteChoisie.EstVisible = true;
            }

            await Task.Delay(400);
            int indexCible = Array.FindIndex(GrilleAdversaire, c => !c.EstVisible && !c.EstVide);
            if (indexCible == -1) indexCible = Array.FindIndex(GrilleAdversaire, c => !c.EstVide);

            var conteneurBouton = ItemsGrilleIA.ItemContainerGenerator.ContainerFromIndex(indexCible) as FrameworkElement;
            var boutonCible = GetVisualChild<Button>(conteneurBouton);

            if (boutonCible != null)
            {
                await DeplacerCurseurVers(boutonCible); await Task.Delay(300);
                Carte ancienne = GrilleAdversaire[indexCible]; ancienne.EstVisible = true; jeu.Defausse.Push(ancienne); GrilleAdversaire[indexCible] = carteChoisie;
            }
            else if (!prendDefausse) { await DeplacerCurseurVers(BtnDefausse); jeu.Defausse.Push(carteChoisie); }

            RafraichirGrilles(); VerifierColonnes(GrilleAdversaire);
            if (PartieEstFinie(GrilleAdversaire)) { CurseurIA.Visibility = Visibility.Hidden; TerminerPartie("L'adversaire a"); return; }
            await Task.Delay(500); CurseurIA.Visibility = Visibility.Hidden; tourDuJoueur = true;
        }

        private static T GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            if (parent == null) return null;
            T child = default; int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T ?? GetVisualChild<T>(v); if (child != null) break;
            }
            return child;
        }
    }
}