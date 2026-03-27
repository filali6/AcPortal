import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject ,Subject} from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {

  private hubConnection!: signalR.HubConnection;
  private isConnected=false;
  
  // Liste des notifications reçues
  notifications$ = new BehaviorSubject<{ message: string, projectId: string }[]>([]);
  toast$ = new Subject<string>();

  startConnection(userId: string): void {
    if ( this.isConnected) return;
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiUrl.replace('/api', '')}/hubs/notifications`)
      .withAutomaticReconnect()
      .build();

     
    this.hubConnection.on('NewNotification', (data) => {
      const current = this.notifications$.value;
      this.notifications$.next([data, ...current]);
      this.toast$.next(data.message);
    });

    
    this.hubConnection.onreconnected(async () => {
      await this.hubConnection.invoke('JoinUserGroup', userId);
    });


    this.hubConnection.start()
      .then(() => {
        // Rejoindre le groupe personnel
        this.hubConnection.invoke('JoinUserGroup', userId);
      })
      .catch(err => console.error('SignalR error:', err));

    // Écouter les nouvelles notifications
    this.hubConnection.on('NewNotification', (data) => {
      const current = this.notifications$.value;
      this.notifications$.next([data, ...current]);
    });
  }

  stopConnection(): void {
    this.hubConnection?.stop();
  }
}