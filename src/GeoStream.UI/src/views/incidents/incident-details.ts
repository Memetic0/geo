import { ICustomElementViewModel } from 'aurelia';
import { route } from '@aurelia/router';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import * as L from 'leaflet';

export interface IncidentDetail {
  id: string;
  state: string;
  severity: string;
  latitude: number;
  longitude: number;
  sensorStationId: string;
  assignedResponderId?: string;
  raisedAt: string;
}

export interface IncidentEvent {
  eventType: string;
  data: string;
  occurredAt: string;
  version: number;
}

export interface IncidentHistory {
  incidentId: string;
  events: IncidentEvent[];
}

@route({
  path: 'incidents/:id',
  title: 'Incident Details'
})
export class IncidentDetails implements ICustomElementViewModel {
  public incident?: IncidentDetail;
  public history?: IncidentHistory;
  public activeTab: 'overview' | 'timeline' | 'map' = 'overview';
  public loading = true;
  public error?: string;
  
  private connection?: HubConnection;
  private map?: L.Map;
  private marker?: L.Marker;
  private mapHost?: HTMLElement;

  async load(params: { id: string }): Promise<void> {
    this.loading = true;
    this.error = undefined;
    
    try {
      await Promise.all([
        this.loadIncident(params.id),
        this.loadHistory(params.id)
      ]);
      
      await this.startRealtime();
    } catch (error) {
      console.error('Failed to load incident details', error);
      this.error = 'Failed to load incident details';
    } finally {
      this.loading = false;
    }
  }

  async detaching(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
    }
    
    this.map?.remove();
    this.marker = undefined;
  }

  private async loadIncident(id: string): Promise<void> {
    const response = await fetch(`/api/incidents/${id}`);
    if (!response.ok) {
      throw new Error(`Failed to fetch incident (${response.status})`);
    }
    
    const data = await response.json();
    this.incident = {
      ...data,
      state: typeof data.state === 'number' ? this.getStateName(data.state) : data.state,
      severity: typeof data.severity === 'number' ? this.getSeverityName(data.severity) : data.severity
    };
  }

  private async loadHistory(id: string): Promise<void> {
    const response = await fetch(`/api/incidents/${id}/history`);
    if (!response.ok) {
      throw new Error(`Failed to fetch history (${response.status})`);
    }
    
    const data = await response.json();
    // Sort events in descending order (most recent first)
    this.history = {
      ...data,
      events: data.events.sort((a: IncidentEvent, b: IncidentEvent) => b.version - a.version)
    };
  }

  private async startRealtime(): Promise<void> {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/incidents')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.on('incidentUpdated', (summary: any) => {
      if (summary.id === this.incident?.id) {
        this.incident = {
          ...summary,
          state: typeof summary.state === 'number' ? this.getStateName(summary.state) : summary.state,
          severity: typeof summary.severity === 'number' ? this.getSeverityName(summary.severity) : summary.severity
        };
        
        // Reload history to get new events
        if (this.incident?.id) {
          this.loadHistory(this.incident.id).catch(console.error);
        }
        
        // Update marker if on map tab
        if (this.activeTab === 'map') {
          this.updateMarker();
        }
      }
    });

    try {
      await this.connection.start();
    } catch (error) {
      console.error('SignalR connection failed', error);
    }
  }

  public switchTab(tab: 'overview' | 'timeline' | 'map'): void {
    this.activeTab = tab;
    
    if (tab === 'map') {
      // Delay to ensure DOM is ready
      setTimeout(() => this.initializeMap(), 100);
    }
  }

  private initializeMap(): void {
    if (!this.incident || this.map) return;
    
    this.mapHost = document.getElementById('detail-map') ?? undefined;
    if (!this.mapHost) {
      console.warn('Map host element not found');
      return;
    }

    this.map = L.map(this.mapHost).setView(
      [this.incident.latitude, this.incident.longitude],
      14
    );

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      maxZoom: 19
    }).addTo(this.map);

    this.updateMarker();
  }

  private updateMarker(): void {
    if (!this.map || !this.incident) return;

    const position: [number, number] = [this.incident.latitude, this.incident.longitude];
    const severityColor = this.getSeverityColor(this.incident.severity);
    
    const icon = L.divIcon({
      className: 'custom-marker',
      html: `<div style="background-color: ${severityColor}; width: 30px; height: 30px; border-radius: 50%; border: 4px solid white; box-shadow: 0 3px 8px rgba(0,0,0,0.4);"></div>`,
      iconSize: [30, 30],
      iconAnchor: [15, 15]
    });

    if (this.marker) {
      this.marker.setLatLng(position);
      this.marker.setIcon(icon);
    } else {
      this.marker = L.marker(position, { icon });
      this.marker.addTo(this.map);
    }
  }

  public parseEventData(event: IncidentEvent): any {
    try {
      return JSON.parse(event.data);
    } catch {
      return {};
    }
  }

  public formatEventData(event: IncidentEvent): string {
    try {
      const parsed = JSON.parse(event.data);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return event.data;
    }
  }

  public getEventProperties(event: IncidentEvent): Array<{ key: string; value: string }> {
    try {
      const data = JSON.parse(event.data);
      const properties: Array<{ key: string; value: string }> = [];

      for (const [key, value] of Object.entries(data)) {
        if (key === '$type') continue; // Skip .NET type metadata
        
        let formattedValue = '';
        if (value === null || value === undefined) {
          formattedValue = 'N/A';
        } else if (typeof value === 'object' && value instanceof Date) {
          formattedValue = new Date(value).toLocaleString();
        } else if (typeof value === 'string' && this.isISODate(value)) {
          formattedValue = new Date(value).toLocaleString();
        } else if (typeof value === 'number') {
          formattedValue = this.formatNumberValue(key, value);
        } else if (typeof value === 'boolean') {
          formattedValue = value ? '✓ Yes' : '✗ No';
        } else {
          formattedValue = String(value);
        }

        properties.push({
          key: this.formatPropertyKey(key),
          value: formattedValue
        });
      }

      return properties;
    } catch {
      return [];
    }
  }

  private isISODate(str: string): boolean {
    const isoDateRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;
    return isoDateRegex.test(str);
  }

  private formatNumberValue(key: string, value: number): string {
    const lowerKey = key.toLowerCase();
    if (lowerKey.includes('latitude') || lowerKey.includes('longitude')) {
      return value.toFixed(6);
    }
    return value.toString();
  }

  private formatPropertyKey(key: string): string {
    // Convert PascalCase to Title Case with spaces
    return key
      .replace(/([A-Z])/g, ' $1')
      .trim()
      .replace(/^./, str => str.toUpperCase());
  }

  public getEventDescription(event: IncidentEvent): string {
    try {
      const data = JSON.parse(event.data);
      const simpleType = this.extractSimpleEventType(event.eventType);
      
      switch (simpleType) {
        case 'IncidentRaised':
          const severity = data.severity || 'Unknown';
          return `Incident detected at sensor station ${data.sensorStationId} with ${severity} severity`;
        case 'IncidentStateAdvanced':
          const toState = data.toState || 'Unknown';
          return `State changed to ${toState}`;
        case 'ResponderAssigned':
          return `Responder ${data.responderId} assigned to incident`;
        case 'IncidentSeverityChanged':
          const updatedSeverity = data.severity || 'Unknown';
          return `Severity updated to ${updatedSeverity}`;
        default:
          return '';
      }
    } catch (error) {
      console.error('Error parsing event description:', error, event);
      return '';
    }
  }

  public getEventIcon(eventType: string): string {
    const simpleType = this.extractSimpleEventType(eventType);
    
    switch (simpleType) {
      case 'IncidentRaised': return '🚨';
      case 'IncidentStateAdvanced': return '⚡';
      case 'ResponderAssigned': return '👤';
      case 'IncidentSeverityChanged': return '⚠️';
      default: return '📝';
    }
  }

  public getEventTitle(eventType: string): string {
    // Extract simple type name from full .NET type (e.g., "GeoStream.Domain.Events.IncidentRaised, ..." -> "IncidentRaised")
    const simpleType = this.extractSimpleEventType(eventType);
    
    switch (simpleType) {
      case 'IncidentRaised': return 'Incident Raised';
      case 'IncidentStateAdvanced': return 'State Changed';
      case 'ResponderAssigned': return 'Responder Assigned';
      case 'IncidentSeverityChanged': return 'Severity Changed';
      default: return simpleType;
    }
  }

  private extractSimpleEventType(eventType: string): string {
    // Handle full .NET type names like "GeoStream.Domain.Events.IncidentRaised, GeoStream.Domain, Version=..."
    if (eventType.includes(',')) {
      const parts = eventType.split(',')[0].split('.');
      return parts[parts.length - 1];
    }
    return eventType;
  }

  public formatDate(dateString: string): string {
    return new Date(dateString).toLocaleString();
  }

  public formatTime(dateString: string): string {
    return new Date(dateString).toLocaleTimeString();
  }

  public getStateColor(state: string): string {
    switch (state) {
      case 'Detected': return '#f59e0b';
      case 'Validated': return '#3b82f6';
      case 'Mitigating': return '#8b5cf6';
      case 'Monitoring': return '#06b6d4';
      case 'Resolved': return '#10b981';
      default: return '#6b7280';
    }
  }

  private getSeverityColor(severity: string): string {
    switch (severity) {
      case 'Critical': return '#dc2626';
      case 'High': return '#ea580c';
      case 'Moderate': return '#ca8a04';
      case 'Low': return '#2563eb';
      default: return '#6b7280';
    }
  }

  private getStateName(stateValue: number): string {
    const states = ['Detected', 'Validated', 'Mitigating', 'Monitoring', 'Resolved'];
    return states[stateValue] ?? 'Unknown';
  }

  private getSeverityName(severityValue: number): string {
    const severities = ['Low', 'Moderate', 'High', 'Critical'];
    return severities[severityValue] ?? 'Unknown';
  }

  public async validateIncident(): Promise<void> {
    await this.advanceIncident('Validate', null);
  }

  public async assignResponder(): Promise<void> {
    const responderId = prompt('Enter Responder ID (e.g., RESP-001):');
    if (!responderId) return;
    await this.advanceIncident('Validate', responderId);
  }

  public async beginMitigation(): Promise<void> {
    await this.advanceIncident('BeginMitigation', null);
  }

  public async beginMonitoring(): Promise<void> {
    await this.advanceIncident('BeginMonitoring', null);
  }

  public async resolveIncident(): Promise<void> {
    await this.advanceIncident('Resolve', null);
  }

  private async advanceIncident(action: string, responderId: string | null): Promise<void> {
    if (!this.incident) return;
    
    try {
      const response = await fetch(`/api/incidents/${this.incident.id}/advance`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ action, responderId })
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to advance incident (${response.status}): ${errorText}`);
      }

      console.log(`Advanced incident ${this.incident.id} with action ${action}`);
      
      // Reload incident and history to show updated state
      const incidentId = this.incident.id;
      await Promise.all([
        this.loadIncident(incidentId),
        this.loadHistory(incidentId)
      ]);
    } catch (error) {
      console.error('Failed to advance incident', error);
      alert(`Failed to ${action} incident. Check console for details.`);
    }
  }

  public canValidate(): boolean {
    return this.incident?.state === 'Detected' && this.incident.assignedResponderId != null;
  }

  public canBeginMitigation(): boolean {
    return this.incident?.state === 'Validated' && this.incident.assignedResponderId != null;
  }

  public canBeginMonitoring(): boolean {
    return this.incident?.state === 'Mitigating';
  }

  public canResolve(): boolean {
    return this.incident?.state === 'Monitoring';
  }
}
