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
            }
        }

        public string Affichage => EstVide ? "" : (EstVisible ? Valeur.ToString() : "?");

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