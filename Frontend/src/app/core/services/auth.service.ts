import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable,tap } from 'rxjs';
import { environment } from '../../../environments/environment';

interface LoginResponse{
  message:string;
  token:string;
}
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl=environment.apiUrl;

  constructor(private http:HttpClient,private router:Router) {}
  login(email:string,password:string):Observable<LoginResponse>{
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login`,{
      email,password
    }).pipe(tap ( response=>{localStorage.setItem('token',response.token);

    })
  );
  }
  logout():void{
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }
  getToken():string |null {
    return localStorage.getItem('token');
  }
  isLoggedIn():boolean{
    return this.getToken() !==null;
  }
  getUserInfo():any{
    const token=this.getToken();
    if (!token) return null;
    const payload = token.split('.')[1];
    return JSON.parse(atob(payload));
  }
  
}
