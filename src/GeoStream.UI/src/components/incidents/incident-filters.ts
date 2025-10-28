import { bindable, customElement } from 'aurelia';

@customElement('incident-filters')
export class IncidentFilters {
  @bindable searchTerm = '';
  @bindable filterType = 'all';
  @bindable filterSeverity = 'all';
  @bindable filterState = 'all';
  @bindable sortBy: 'date' | 'severity' | 'state' = 'date';
  @bindable sortDirection: 'asc' | 'desc' = 'desc';
  @bindable onSearchChange!: () => void;
  @bindable onFilterChange!: () => void;
  @bindable setSortBy!: (sortBy: string) => void;

  public clearSearch(): void {
    this.searchTerm = '';
    if (this.onSearchChange) {
      this.onSearchChange();
    }
  }
}
