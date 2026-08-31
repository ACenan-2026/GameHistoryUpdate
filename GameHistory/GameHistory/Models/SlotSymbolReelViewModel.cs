using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameHistory.Models
{
  public class SlotSymbolReelViewModel
  {
    public string Id { get; set; }
    public List<SlotSymbolViewModel> Floors { get; set; }

    public SlotSymbolReelViewModel(string id)
    {
      this.Id = id;
    }
  }
}
