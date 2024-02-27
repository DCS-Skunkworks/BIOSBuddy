using BIOSBuddy.Events;

namespace BIOSBuddy.Interfaces
{
    internal interface ICategoryChange
    {
        void ChangeCategory(object sender, CategoryEventArgs args);
    }
    internal interface INewDCSBIOSData
    {
        void NewDCSBIOSData(object sender, DCSBIOSDataCombinedEventArgs args);
    }
}
