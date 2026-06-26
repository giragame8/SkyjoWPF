using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SkyjoWPF.Core;

namespace SkyjoWPF
{
    public enum DifficulteIA { Facile, Normal }

    public partial class MainWindow : Window
    {
        private readonly MoteurJeu jeu;
        private Carte carteEnMain;
        private bool tourDuJoueur = true;
        private readonly DifficulteIA difficulte = DifficulteIA.Normal;

        private readonly Carte[] GrilleIA = new Carte[12];

        public MainWindow()
        {
            InitializeComponent();
            jeu = new MoteurJeu();
            InitialiserInterface();
        }

        private void InitialiserInterface()
        {
            for (int i = 0; i < 12; i++) GrilleIA[i] = jeu.TirerCarte();

            ItemsGrille.ItemsSource = jeu.GrilleJoueur;
            ItemsGrilleIA.ItemsSource = GrilleIA;
            MettreAJourDefausse();
        }

        private void MettreAJourDefausse()
        {
            if (jeu.Defausse.Count > 0)
                BtnDefausse.Content = jeu.Defausse.Peek().Valeur.ToString();
        }

        private void RafraichirGrilles()
        {
            ItemsGrille.ItemsSource = null;
            ItemsGrille.ItemsSource = jeu.GrilleJoueur;
            ItemsGrilleIA.ItemsSource = null;
            ItemsGrilleIA.ItemsSource = GrilleIA;
            MettreAJourDefausse();
        }

        private void VerifierColonnes(Carte[] grille)
        {
            for (int c = 0; c < 4; c++)
            {
                Carte c1 = grille[c];
                Carte c2 = grille[c + 4];
                Carte c3 = grille[c + 8];

                if (!c1.EstVide && !c2.EstVide && !c3.EstVide &&
                    c1.EstVisible && c2.EstVisible && c3.EstVisible &&
                    c1.Valeur == c2.Valeur && c2.Valeur == c3.Valeur)
                {
                    jeu.Defausse.Push(c1);
                    c1.EstVide = true;
                    c2.EstVide = true;
                    c3.EstVide = true;
                }
            }
        }

        private void TerminerPartie(string declencheur)
        {
            tourDuJoueur = false;

            foreach (var c in jeu.GrilleJoueur) c.EstVisible = true;
            foreach (var c in GrilleIA) c.EstVisible = true;

            VerifierColonnes(jeu.GrilleJoueur);
            VerifierColonnes(GrilleIA);
            RafraichirGrilles();

            int scoreJoueur = jeu.GrilleJoueur.Where(c => !c.EstVide).Sum(c => c.Valeur);
            int scoreIA = GrilleIA.Where(c => !c.EstVide).Sum(c => c.Valeur);

            string message = $"{declencheur} retourné toutes ses cartes !\n\n" +
                             $"Votre score : {scoreJoueur}\n" +
                             $"Score IA : {scoreIA}\n\n";

            if (scoreJoueur < scoreIA) message += "🏆 VOUS AVEZ GAGNÉ !";
            else if (scoreJoueur > scoreIA) message += "💀 L'IA A GAGNÉ !";
            else message += "🤝 ÉGALITÉ !";

            MessageBox.Show(message, "Fin de la partie");
        }

        private bool PartieEstFinie(Carte[] grille)
        {
            return grille.All(c => c.EstVide || c.EstVisible);
        }

        private void BtnPioche_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur) return;

            if (carteEnMain == null)
            {
                carteEnMain = jeu.TirerCarte();
                carteEnMain.EstVisible = true;
                MessageBox.Show($"Vous avez pioché : {carteEnMain.Valeur}\nCliquez sur une de vos cartes pour la remplacer.", "Pioche");
            }
        }

        private void BtnDefausse_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur) return;

            if (carteEnMain == null && jeu.Defausse.Count > 0)
            {
                carteEnMain = jeu.Defausse.Pop();
                MettreAJourDefausse();
                MessageBox.Show($"Vous avez pris le {carteEnMain.Valeur} de la défausse.\nCliquez sur une de vos cartes pour la remplacer.", "Défausse");
            }
        }

        private async void Carte_Click(object sender, RoutedEventArgs e)
        {
            if (!tourDuJoueur) return;

            Button btn = sender as Button;
            Carte carteCliquee = btn?.DataContext as Carte;

            if (carteCliquee != null && !carteCliquee.EstVide)
            {
                if (carteEnMain != null)
                {
                    int index = Array.IndexOf(jeu.GrilleJoueur, carteCliquee);

                    carteCliquee.EstVisible = true;
                    jeu.Defausse.Push(carteCliquee);

                    jeu.GrilleJoueur[index] = carteEnMain;
                    carteEnMain = null;
                }
                else if (!carteCliquee.EstVisible)
                {
                    carteCliquee.EstVisible = true;
                }
                else return;

                RafraichirGrilles();
                VerifierColonnes(jeu.GrilleJoueur);

                if (PartieEstFinie(jeu.GrilleJoueur))
                {
                    TerminerPartie("Vous avez");
                    return;
                }

                await JouerTourIA();
            }
        }

        private Task DeplacerCurseurVers(UIElement cible)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (cible == null)
            {
                tcs.TrySetResult(true);
                return tcs.Task;
            }

            Point position = cible.TranslatePoint(new Point(cible.RenderSize.Width / 2 - 20, cible.RenderSize.Height / 2 - 20), OverlayCanvas);

            CurseurIA.Visibility = Visibility.Visible;

            DoubleAnimation animX = new DoubleAnimation { To = position.X, Duration = TimeSpan.FromSeconds(0.6), EasingFunction = new QuadraticEase() };
            DoubleAnimation animY = new DoubleAnimation { To = position.Y, Duration = TimeSpan.FromSeconds(0.6), EasingFunction = new QuadraticEase() };

            animX.Completed += (s, ev) => tcs.TrySetResult(true);

            CurseurIA.BeginAnimation(Canvas.LeftProperty, animX);
            CurseurIA.BeginAnimation(Canvas.TopProperty, animY);

            return tcs.Task;
        }

        private async Task JouerTourIA()
        {
            tourDuJoueur = false;
            await Task.Delay(1000);

            Carte carteChoisie = null;
            bool prendDefausse = false;

            if (jeu.Defausse.Count > 0 && jeu.Defausse.Peek().Valeur <= 4 && difficulte == DifficulteIA.Normal)
            {
                await DeplacerCurseurVers(BtnDefausse);
                carteChoisie = jeu.Defausse.Pop();
                prendDefausse = true;
            }
            else
            {
                await DeplacerCurseurVers(BtnPioche);
                carteChoisie = jeu.TirerCarte();
                carteChoisie.EstVisible = true;
            }

            await Task.Delay(400);

            int indexCible = Array.FindIndex(GrilleIA, c => !c.EstVisible && !c.EstVide);
            if (indexCible == -1) indexCible = Array.FindIndex(GrilleIA, c => !c.EstVide);

            var conteneurBouton = ItemsGrilleIA.ItemContainerGenerator.ContainerFromIndex(indexCible) as FrameworkElement;
            var boutonCible = GetVisualChild<Button>(conteneurBouton);

            if (boutonCible != null)
            {
                await DeplacerCurseurVers(boutonCible);
                await Task.Delay(300);

                Carte ancienne = GrilleIA[indexCible];
                ancienne.EstVisible = true;
                jeu.Defausse.Push(ancienne);
                GrilleIA[indexCible] = carteChoisie;
            }
            else if (!prendDefausse)
            {
                await DeplacerCurseurVers(BtnDefausse);
                jeu.Defausse.Push(carteChoisie);
            }

            RafraichirGrilles();
            VerifierColonnes(GrilleIA);

            if (PartieEstFinie(GrilleIA))
            {
                CurseurIA.Visibility = Visibility.Hidden;
                TerminerPartie("L'adversaire a");
                return;
            }

            await Task.Delay(500);
            CurseurIA.Visibility = Visibility.Hidden;
            tourDuJoueur = true;
        }

        private static T GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            if (parent == null) return null;
            T child = default;
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T ?? GetVisualChild<T>(v);
                if (child != null) break;
            }
            return child;
        }
    }
}