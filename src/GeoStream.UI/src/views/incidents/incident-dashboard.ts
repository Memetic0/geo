import { ICustomElementViewModel } from 'aurelia';
import { route } from '@aurelia/router';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { IncidentMap } from '../../components/incidents/incident-map';
import { IncidentFilters } from '../../components/incidents/incident-filters';
import { IncidentTableRows } from '../../components/incidents/incident-table-rows';
import { IncidentModals } from '../../components/incidents/incident-modals';

export interface IncidentSummary {
  id: string;
  type: string;
  state: string;
  severity: string;
  latitude: number;
  longitude: number;
  sensorStationId: string;
  assignedResponderId?: string;
  raisedAt: string;
}

@route({ path: ['', 'incidents'], title: 'Incident Dashboard' })
export class IncidentDashboard implements ICustomElementViewModel {
  static dependencies = [IncidentMap, IncidentFilters, IncidentTableRows, IncidentModals];
  public incidents: IncidentSummary[] = [];
  public filteredIncidents: IncidentSummary[] = [];
  public searchTerm = '';
  public sortBy: 'date' | 'severity' | 'state' = 'date';
  public sortDirection: 'asc' | 'desc' = 'desc';
  public filterSeverity: string = 'all';
  public filterState: string = 'all';
  public filterType: string = 'all';

  // Incident creation modal
  public showCreateModal = false;
  public newIncident = {
    latitude: 0,
    longitude: 0,
    type: 'TrafficCongestion',
    severity: 'Moderate',
    sensorStationId: 'SENS-001'
  };

  // Responder assignment modal
  public showAssignModal = false;
  public assigningIncident: IncidentSummary | null = null;
  public selectedResponderId = 'RESP-001';
  public availableResponders = ['RESP-001', 'RESP-002', 'RESP-003', 'RESP-004', 'RESP-005'];

  // Severity change modal
  public showSeverityModal = false;
  public changingSeverityIncident: IncidentSummary | null = null;
  public newSeverity = 'Moderate';

  private connection?: HubConnection;
  public mapComponent?: IncidentMap;
  public selectedIncidentId: string | null = null;
  public hoveredIncidentId: string | null = null;

  async attached(): Promise<void> {
    await this.refresh();
    await this.startRealtime();
  }

  async detaching(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
    }
  }

  public async refresh(): Promise<void> {
    try {
      // Build query parameters
      const params = new URLSearchParams();
      
      if (this.searchTerm) {
        params.append('searchTerm', this.searchTerm);
      }
      
      if (this.filterSeverity && this.filterSeverity !== 'all') {
        params.append('severity', this.filterSeverity);
      }
      
      if (this.filterState && this.filterState !== 'all') {
        params.append('state', this.filterState);
      }
      
      if (this.filterType && this.filterType !== 'all') {
        // Convert kebab-case to PascalCase for the API
        const typeValue = this.normalizeFilterType(this.filterType);
        params.append('type', typeValue);
      }
      
      params.append('page', '1');
      params.append('pageSize', '1000');
      
      const url = `/api/incidents/search?${params.toString()}`;
      const response = await fetch(url);
      if (!response.ok) {
        throw new Error(`Failed to fetch incidents (${response.status})`);
      }

      const result = await response.json();
      const rawIncidents = result.incidents || [];
      this.incidents = rawIncidents.map((raw: any) => this.normalizeIncident(raw));
      this.filteredIncidents = [...this.incidents];
      this.applySort();
    } catch (error) {
      console.error('Incident fetch failed', error);
    }
  }

  public onSearchChange = (): void => {
    this.refresh();
  }

  public clearSearch = (): void => {
    this.searchTerm = '';
    this.refresh();
  }

  public clearAllFilters = (): void => {
    this.searchTerm = '';
    this.filterSeverity = 'all';
    this.filterState = 'all';
    this.filterType = 'all';
    this.refresh();
  }

  public setSortBy = (sortBy: 'date' | 'severity' | 'state'): void => {
    if (this.sortBy === sortBy) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = sortBy;
      this.sortDirection = 'desc';
    }
    this.applySort();
  }

  public onFilterChange = (): void => {
    this.refresh();
  }

  private normalizeFilterType(filterType: string): string {
    // Convert kebab-case to PascalCase for type matching
    return filterType.split('-').map(word => 
      word.charAt(0).toUpperCase() + word.slice(1)
    ).join('');
  }

  private applySort(): void {
    const sorted = [...this.filteredIncidents];

    // Apply sorting
    sorted.sort((a, b) => {
      let comparison = 0;

      switch (this.sortBy) {
        case 'date':
          comparison = new Date(a.raisedAt).getTime() - new Date(b.raisedAt).getTime();
          break;
        case 'severity':
          const severityOrder = { 'Critical': 4, 'High': 3, 'Moderate': 2, 'Low': 1 };
          comparison = (severityOrder[a.severity as keyof typeof severityOrder] || 0) - 
                      (severityOrder[b.severity as keyof typeof severityOrder] || 0);
          break;
        case 'state':
          const stateOrder = { 'Detected': 1, 'Validated': 2, 'Mitigating': 3, 'Monitoring': 4, 'Resolved': 5 };
          comparison = (stateOrder[a.state as keyof typeof stateOrder] || 0) - 
                      (stateOrder[b.state as keyof typeof stateOrder] || 0);
          break;
      }

      return this.sortDirection === 'asc' ? comparison : -comparison;
    });

    this.filteredIncidents = sorted;
  }

  get hasIncidents(): boolean {
    return this.filteredIncidents.length > 0;
  }

  get totalIncidents(): number {
    // Count only non-resolved incidents as "live"
    return this.incidents.filter(i => i.state !== 'Resolved').length;
  }

  get filteredCount(): number {
    return this.filteredIncidents.length;
  }

  public formatDate(dateString: string): string {
    return new Date(dateString).toLocaleString();
  }

  public canIntervene(incident: IncidentSummary): boolean {
    return incident.state !== 'Resolved';
  }

  public handleMapClick(event: { lat: number; lng: number }): void {
    // Set the coordinates for the new incident
    if (this.newIncident) {
      this.newIncident.latitude = event.lat;
      this.newIncident.longitude = event.lng;
    }
    
    // Show create modal
    this.showCreateModal = true;
  }

  public async createIncident(event?: Event): Promise<void> {
    if (event) {
      event.stopPropagation();
    }
    
    try {
      const response = await fetch('/api/incidents', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          type: this.newIncident.type,
          latitude: this.newIncident.latitude,
          longitude: this.newIncident.longitude,
          severity: this.newIncident.severity,
          sensorStationId: this.newIncident.sensorStationId
        })
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to create incident (${response.status}): ${errorText}`);
      }

      const incidentId = await response.text();
      console.log(`Created incident ${incidentId}`);
      
      // Close modal
      this.closeCreateModal();
      
      // Refresh to show the new incident
      await this.refresh();
    } catch (error) {
      console.error('Failed to create incident', error);
      alert('Failed to create incident. Check console for details.');
    }
  }

  public closeCreateModal(): void {
    this.showCreateModal = false;
    
    // Reset form
    this.newIncident = {
      latitude: 0,
      longitude: 0,
      type: 'TrafficCongestion',
      severity: 'Moderate',
      sensorStationId: 'SENS-001'
    };
  }

  public assignResponder(incident: IncidentSummary): void {
    this.assigningIncident = incident;
    this.selectedResponderId = 'RESP-001';
    this.showAssignModal = true;
  }

  public async confirmAssignResponder(): Promise<void> {
    if (!this.assigningIncident) return;

    await this.advanceIncident(this.assigningIncident.id, 'Validate', this.selectedResponderId);
    this.closeAssignModal();
  }

  public closeAssignModal(): void {
    this.showAssignModal = false;
    this.assigningIncident = null;
    this.selectedResponderId = 'RESP-001';
  }

  public openSeverityModal(incident: IncidentSummary): void {
    this.changingSeverityIncident = incident;
    this.newSeverity = incident.severity;
    this.showSeverityModal = true;
  }

  public async confirmChangeSeverity(): Promise<void> {
    if (!this.changingSeverityIncident) return;

    try {
      const response = await fetch(`/api/incidents/${this.changingSeverityIncident.id}/severity`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ severity: this.newSeverity })
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to update severity (${response.status}): ${errorText}`);
      }

      console.log(`Updated severity for incident ${this.changingSeverityIncident.id}`);
      this.closeSeverityModal();
      await this.refresh();
    } catch (error) {
      console.error('Failed to update severity', error);
      alert('Failed to update severity. Check console for details.');
    }
  }

  public closeSeverityModal(): void {
    this.showSeverityModal = false;
    this.changingSeverityIncident = null;
    this.newSeverity = 'Moderate';
  }

  public onRowHover = (incident: IncidentSummary): void => {
    this.hoveredIncidentId = incident.id;
  }

  public onRowLeave = (): void => {
    this.hoveredIncidentId = null;
  }

  public onRowClick = (incident: IncidentSummary): void => {
    this.selectedIncidentId = incident.id;
    // Delegate to map component for zoom and show popup
    if (this.mapComponent) {
      this.mapComponent.zoomToIncident(incident.latitude, incident.longitude);
      // Also trigger the marker click to show popup
      this.mapComponent.showIncidentPopup(incident.id);
    }
  }

  public resetZoom(event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    this.selectedIncidentId = null;
    // Delegate to map component
    if (this.mapComponent) {
      this.mapComponent.resetZoom();
    }
  }

  public async validateIncident(incident: IncidentSummary): Promise<void> {
    await this.advanceIncident(incident.id, 'Validate', null);
  }

  public async beginMitigation(incident: IncidentSummary): Promise<void> {
    await this.advanceIncident(incident.id, 'BeginMitigation', null);
  }

  public async beginMonitoring(incident: IncidentSummary): Promise<void> {
    await this.advanceIncident(incident.id, 'BeginMonitoring', null);
  }

  public async resolveIncident(incident: IncidentSummary): Promise<void> {
    await this.advanceIncident(incident.id, 'Resolve', null);
  }

  private async advanceIncident(incidentId: string, action: string, responderId: string | null): Promise<void> {
    try {
      const response = await fetch(`/api/incidents/${incidentId}/advance`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ action, responderId })
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to advance incident (${response.status}): ${errorText}`);
      }

      console.log(`Advanced incident ${incidentId} with action ${action}`);
    } catch (error) {
      console.error('Failed to advance incident', error);
      alert(`Failed to ${action} incident. Check console for details.`);
    }
  }

  private async startRealtime(): Promise<void> {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/incidents')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.on('incidentUpdated', (summary: any) => {
      const normalized = this.normalizeIncident(summary);
      this.upsertIncident(normalized);
      // Refresh from backend to ensure filters are applied correctly
      this.refresh();
    });

    try {
      await this.connection.start();
    } catch (error) {
      console.error('SignalR connection failed', error);
    }
  }

  private upsertIncident(summary: IncidentSummary): void {
    const existingIndex = this.incidents.findIndex(i => i.id === summary.id);
    if (existingIndex >= 0) {
      this.incidents = this.incidents.map((incident, index) =>
        index === existingIndex ? summary : incident
      );
    } else {
      this.incidents = [summary, ...this.incidents];
    }
  }

  private normalizeIncident(raw: any): IncidentSummary {
    // Convert enum numbers to strings if needed
    const severityMap = ['Low', 'Moderate', 'High', 'Critical'];
    const stateMap = ['Detected', 'Validated', 'Mitigating', 'Monitoring', 'Resolved'];
    const typeMap = [
      'TrafficCongestion', 'RoadAccident', 'RoadClosure',
      'VehicleBreakdown', 'Roadwork', 'PublicTransportDelay',
      'ParkingViolation', 'SignalMalfunction', 'PedestrianIncident', 'StreetFlooding'
    ];

    return {
      id: raw.id,
      type: typeof raw.type === 'number' ? typeMap[raw.type] : raw.type,
      state: typeof raw.state === 'number' ? stateMap[raw.state] : raw.state,
      severity: typeof raw.severity === 'number' ? severityMap[raw.severity] : raw.severity,
      latitude: raw.latitude,
      longitude: raw.longitude,
      sensorStationId: raw.sensorStationId,
      assignedResponderId: raw.assignedResponderId,
      raisedAt: raw.raisedAt
    };
  }
}
