import{d,x as o,o as i,e as c,l as p,_ as l}from"./index-B7jvAjdE.js";import{a as n}from"./broadcasts-BpyehJtv.js";import{P as u}from"./PlayerListCard-CmEr-veR.js";const _=d({__name:"BroadcastPlayerTargetCard",props:{broadcastId:{}},setup(t,{expose:s}){s();const a=t,{data:e}=o(n(a.broadcastId)),r={props:a,data:e,get useSubscription(){return o},get getSingleBroadcastSubscriptionOptions(){return n},PlayerListCard:u};return Object.defineProperty(r,"__isScriptSetup",{enumerable:!1,value:!0}),r}});function m(t,s,a,e,r,g){return e.data?.message?(i(),c(e.PlayerListCard,{key:0,playerIds:e.data.message.params.targetPlayers??[],title:"Individual Players",emptyMessage:"No individual players targeted."},null,8,["playerIds"])):p("",!0)}const b=l(_,[["render",m],["__file","BroadcastPlayerTargetCard.vue"]]);export{b as default};
