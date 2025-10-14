// 檔案：ViewModels/ViewModelBase.cs

using System.ComponentModel; // 引用 INotifyPropertyChanged 介面
using System.Runtime.CompilerServices; // 引用 CallerMemberName 屬性

namespace collect_all.ViewModels // 請確保命名空間與您的專案一致
{
    /// <summary>
    /// 所有 ViewModel 的基底類別，用於實作 INotifyPropertyChanged 介面。
    /// 這讓 View 可以偵測到 ViewModel 中屬性值的變化並自動更新。
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        // 這是 INotifyPropertyChanged 介面要求實作的事件。
        // 當 ViewModel 的屬性值改變時，會觸發這個事件，通知所有訂閱的 View。
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 觸發 PropertyChanged 事件的輔助方法。
        /// </summary>
        /// <param name="propertyName">發生變化的屬性名稱。
        /// [CallerMemberName] 屬性會自動在編譯時將呼叫此方法的屬性名稱填入，無需手動傳遞。</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // 如果有任何 View 訂閱了 PropertyChanged 事件，則觸發它。
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 我們不需要 Dispose 相關的邏輯，因為這個範例中的 ViewModel 沒有需要清理的資源。
        // 但在更複雜的應用程式中，ViewModelBase 可能會實作 IDisposable 介面。
        public virtual void Dispose() { }
    }
}