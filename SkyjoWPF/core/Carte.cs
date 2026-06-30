using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SkyjoWPF.Core
{
    public class Carte : INotifyPropertyChanged
    {
        private bool _estVisible;
        private bool _estVide;

        public int Valeur { get; private set; }

        public bool EstVisible
        {
            get { return _estVisible; }
            set
            {
                _estVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Affichage));
                OnPropertyChanged(nameof(CouleurFond));
                OnPropertyChanged(nameof(CouleurTexte));
            }
        }

        public bool EstVide
        {
            get { return _estVide; }
            set
            {
                _estVide = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Affichage));
                OnPropertyChanged(nameof(CouleurFond));
                OnPropertyChanged(nameof(CouleurTexte));
            }
        }

        public string Affichage => EstVide ? "" : (EstVisible ? Valeur.ToString() : "?");

        // Assigne les couleurs officielles du jeu Skyjo
        public string CouleurFond
        {
            get
            {
                if (EstVide) return "Transparent";
                if (!EstVisible) return "#34495e"; // Couleur du dos de la carte (Bleu/Gris foncé)

                if (Valeur == -2) return "#9b59b6"; // Violet
                if (Valeur == -1) return "#2980b9"; // Bleu foncé
                if (Valeur == 0) return "#3498db";  // Bleu clair
                if (Valeur >= 1 && Valeur <= 4) return "#2ecc71";  // Vert
                if (Valeur >= 5 && Valeur <= 8) return "#f1c40f";  // Jaune
                if (Valeur >= 9 && Valeur <= 12) return "#e74c3c"; // Rouge

                return "#ffffff";
            }
        }

        // Le texte est blanc sur le dos de la carte, et foncé quand la carte est visible pour mieux lire
        public string CouleurTexte => EstVide ? "Transparent" : (EstVisible ? "#2c3e50" : "#ffffff");

        public Carte(int valeur)
        {
            Valeur = valeur;
            EstVisible = false;
            EstVide = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}