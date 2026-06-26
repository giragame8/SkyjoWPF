using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SkyjoWPF.Core;

// Bibliothèques réseau nécessaires
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SkyjoWPF
{
    public enum ModeJeu { Solo, Hote, Client }

    // Petite classe pour stocker les infos des parties trouvées sur le réseau
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
        private bool tourDuJoueur = true;

        private Carte[] GrilleP2 = new Carte[12];
        private ModeJeu modeActuel = ModeJeu.Solo;

        // Variables réseau
        private bool isHostingBroadcast = false;
        private const int PORT_UDP_SCAN = 5556; // Le port utilisé pour se crier dessus sur le réseau

        public MainWindow()
        {
            InitializeComponent();
        }

        // ==========================================
        // GESTION DU MENU ET DU SCANNAGE RÉSEAU
        // ==========================================

        private void BtnSolo_Click(object sender, RoutedEventArgs e)
        {
            modeActuel = ModeJeu.Solo;
            TxtNomAdversaire.Text = "ADVERSAIRE (IA)";
            LancerPartieLocale();
        }

        private void BtnHeberger_Click(object sender, RoutedEventArgs e)
        {
            modeActuel = ModeJeu.Hote;
            TxtNomAdversaire.Text = "JOUEUR 2 (Attente...)";
            TxtStatutMenu.Text = "Hébergement en cours... En attente d'un joueur.";

            // 1. On lance le cri sur le réseau
            isHostingBroadcast = true;
            _ = BroadcasterPresence();

            // 2. ICI PLUS TARD : On ouvrira le vrai serveur TCP (comme dans votre Python) pour que le client se connecte.

            // Pour l'instant, on cache le menu pour voir le plateau vide
            MenuPanel.Visibility = Visibility.Collapsed;
        }

        private void BtnScanner_Click(object sender, RoutedEventArgs e)
        {
            TxtStatutMenu.Text = "Recherche de parties en cours...";
            ListServeurs.Items.Clear();
            BtnScanner_Click_Logic();
        }

        private void BtnRejoindre_Click(object sender, RoutedEventArgs e)
        {
            if (ListServeurs.SelectedItem is ServeurInfo serveurChoisi)
            {
                modeActuel = ModeJeu.Client;
                TxtNomAdversaire.Text = $"HÔTE ({serveurChoisi.NomDuPC})";
                MenuPanel.Visibility = Visibility.Collapsed;

                MessageBox.Show($"Bientôt : Connexion à l'IP {serveurChoisi.IP} en TCP !");
                // ICI PLUS TARD : Connexion TCP au serveur
            }
            else
            {
                MessageBox.Show("Veuillez sélectionner une partie dans la liste !");
            }
        }

        // --- Le code du Scanner (Le Cri) ---
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
                    // On envoie le message à tout le réseau
                    await udp.SendAsync(bytes, bytes.Length, endPoint);
                    await Task.Delay(2000); // Toutes les 2 secondes
                }
            }
        }

        // --- Le code du Scanner (L'Écoute) ---
        private async void BtnScanner_Click_Logic()
        {
            using (UdpClient udp = new UdpClient())
            {
                // On écoute sur le port 5556
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, PORT_UDP_SCAN));

                // On écoute pendant maximum 4 secondes pour ne pas bloquer l'application indéfiniment
                var receiveTask = udp.ReceiveAsync();
                var delayTask = Task.Delay(4000);

                while (true)
                {
                    var completedTask = await Task.WhenAny(receiveTask, delayTask);
                    if (completedTask == delayTask)
                    {
                        break; // Temps écoulé
                    }

                    var result = receiveTask.Result;
                    string msg = Encoding.UTF8.GetString(result.Buffer);

                    if (msg.StartsWith("SKYJO|"))
                    {
                        string nomDuPC = msg.Split('|')[1];
                        string ip = result.RemoteEndPoint.Address.ToString();

                        // On vérifie si on l'a pas déjà ajouté
                        bool dejaPresent = false;
                        foreach (ServeurInfo s in ListServeurs.Items) { if (s.IP == ip) dejaPresent = true; }

                        if (!dejaPresent)
                        {
                            ListServeurs.Items.Add(new ServeurInfo { IP = ip, NomDuPC = nomDuPC });
                        }
                    }

                    // On relance l'écoute pour les autres
                    receiveTask = udp.ReceiveAsync();
                }
            }
            TxtStatutMenu.Text = "Scan terminé.";
        }


        // ==========================================
        // LOGIQUE DU JEU (Reste identique à avant)
        // ==========================================

        private void LancerPartieLocale()
        {
            MenuPanel.Visibility = Visibility.Collapsed;
            jeu = new MoteurJeu();
            for (int i = 0; i < 12; i++) GrilleP2[i] = jeu.TirerCarte();
            ItemsGrille.ItemsSource = jeu.GrilleJoueur;
            ItemsGrilleIA.ItemsSource = GrilleP2;
            MettreAJourDefausse();
        }

        private void MettreAJourDefausse() { if (jeu.Defausse.Count > 0) BtnDefausse.Content = jeu.Defausse.Peek().Valeur.ToString(); }
        private void RafraichirGrilles() { ItemsGrille.ItemsSource = null; ItemsGrille.ItemsSource = jeu.GrilleJoueur; ItemsGrilleIA.ItemsSource = null; ItemsGrilleIA.ItemsSource = GrilleP2; MettreAJourDefausse(); }

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
            foreach (var c in jeu.GrilleJoueur) c.EstVisible = true;
            foreach (var c in GrilleP2) c.EstVisible = true;
            VerifierColonnes(jeu.GrilleJoueur); VerifierColonnes(GrilleP2); RafraichirGrilles();

            int scoreJoueur = jeu.GrilleJoueur.Where(c => !c.EstVide).Sum(c => c.Valeur);
            int scoreIA = GrilleP2.Where(c => !c.EstVide).Sum(c => c.Valeur);

            string message = $"{declencheur} retourné toutes ses cartes !\n\nVotre score : {scoreJoueur}\nScore IA : {scoreIA}\n\n";
            if (scoreJoueur < scoreIA) message += "🏆 VOUS AVEZ GAGNÉ !"; else if (scoreJoueur > scoreIA) message += "💀 L'IA A GAGNÉ !"; else message += "🤝 ÉGALITÉ !";
            MessageBox.Show(message, "Fin de la partie");

            // Retour au menu
            MenuPanel.Visibility = Visibility.Visible;
            isHostingBroadcast = false; // On arrête le réseau si on était hôte
        }

        private bool PartieEstFinie(Carte[] grille) { return grille.All(c => c.EstVide || c.EstVisible); }

        private void BtnPioche_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur || modeActuel != ModeJeu.Solo) return;
            if (carteEnMain == null)
            {
                carteEnMain = jeu.TirerCarte(); carteEnMain.EstVisible = true;
                MessageBox.Show($"Vous avez pioché : {carteEnMain.Valeur}\nCliquez sur une de vos cartes pour la remplacer.", "Pioche");
            }
        }

        private void BtnDefausse_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur || modeActuel != ModeJeu.Solo) return;
            if (carteEnMain == null && jeu.Defausse.Count > 0)
            {
                carteEnMain = jeu.Defausse.Pop(); MettreAJourDefausse();
                MessageBox.Show($"Vous avez pris le {carteEnMain.Valeur} de la défausse.\nCliquez sur une de vos cartes pour la remplacer.", "Défausse");
            }
        }

        private async void Carte_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur || modeActuel != ModeJeu.Solo) return;
            Button btn = sender as Button; Carte carteCliquee = btn?.DataContext as Carte;

            if (carteCliquee != null && !carteCliquee.EstVide)
            {
                if (carteEnMain != null)
                {
                    int index = Array.IndexOf(jeu.GrilleJoueur, carteCliquee);
                    carteCliquee.EstVisible = true; jeu.Defausse.Push(carteCliquee);
                    jeu.GrilleJoueur[index] = carteEnMain; carteEnMain = null;
                }
                else if (!carteCliquee.EstVisible) { carteCliquee.EstVisible = true; }
                else return;

                RafraichirGrilles(); VerifierColonnes(jeu.GrilleJoueur);
                if (PartieEstFinie(jeu.GrilleJoueur)) { TerminerPartie("Vous avez"); return; }
                await JouerTourIA();
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
            int indexCible = Array.FindIndex(GrilleP2, c => !c.EstVisible && !c.EstVide);
            if (indexCible == -1) indexCible = Array.FindIndex(GrilleP2, c => !c.EstVide);

            var conteneurBouton = ItemsGrilleIA.ItemContainerGenerator.ContainerFromIndex(indexCible) as FrameworkElement;
            var boutonCible = GetVisualChild<Button>(conteneurBouton);

            if (boutonCible != null)
            {
                await DeplacerCurseurVers(boutonCible); await Task.Delay(300);
                Carte ancienne = GrilleP2[indexCible]; ancienne.EstVisible = true; jeu.Defausse.Push(ancienne); GrilleP2[indexCible] = carteChoisie;
            }
            else if (!prendDefausse) { await DeplacerCurseurVers(BtnDefausse); jeu.Defausse.Push(carteChoisie); }

            RafraichirGrilles(); VerifierColonnes(GrilleP2);
            if (PartieEstFinie(GrilleP2)) { CurseurIA.Visibility = Visibility.Hidden; TerminerPartie("L'adversaire a"); return; }
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